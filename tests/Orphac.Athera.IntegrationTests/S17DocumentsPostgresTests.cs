using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Documents;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;
using Xunit;

namespace Orphac.Athera.IntegrationTests;

public sealed class S17DocumentsPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S17World(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ControllerId,
        string ControllerEmail,
        Guid ForeignId,
        string ForeignEmail,
        string Password);

    [Fact]
    public async Task S17_PG_01_DocumentsRlsImmutabilityAndRealistic50DocumentLoadQuery()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        // Verify ENABLE + FORCE ROW LEVEL SECURITY on all 3 documents.* tables
        await using (var rlsCmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'documents'
              AND c.relname IN ('document_sequences', 'business_documents', 'document_links')
              AND c.relrowsecurity
              AND c.relforcerowsecurity;
            """, appConn))
        {
            Assert.Equal(3L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Missing tenant GUC sees 0 rows
        await using (var noCtx = new NpgsqlCommand("SELECT COUNT(*) FROM documents.business_documents;", appConn))
        {
            Assert.Equal(0L, Convert.ToInt64(await noCtx.ExecuteScalarAsync()));
        }

        // Insert 50 documents under Tenant A Entity 1 as athera_app and verify sub-second join + immutability
        Guid sampleDocId = Guid.Empty;
        var sw = Stopwatch.StartNew();
        await using (var tx = await appConn.BeginTransactionAsync())
        {
            await using (var setCtx = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, true);", appConn, tx))
            {
                setCtx.Parameters.AddWithValue("t", world.TenantAId.ToString("D"));
                await setCtx.ExecuteNonQueryAsync();
            }

            var payload = Encoding.UTF8.GetBytes("Synthetic evidence payload for realistic load query");
            var sha = DocumentSecurity.ComputeSha256Hex(payload);

            for (var i = 1; i <= 50; i++)
            {
                var docId = Guid.NewGuid();
                if (i == 1) sampleDocId = docId;
                var docNum = $"DOC-2026-{i:D5}";

                await using var insDoc = new NpgsqlCommand("""
                    INSERT INTO documents.business_documents
                        (document_id, tenant_id, legal_entity_id, document_number, file_name, content_type, byte_size, sha256_hash, category, scan_status, content_bytes, notes, uploaded_by_user_id, created_at_utc)
                    VALUES
                        (@id, @t, @e, @num, @fn, 'text/plain', @sz, @sha, 'GENERAL', 'CLEAN', @bytes, 'Load test doc', @u, NOW());
                    """, appConn, tx);
                insDoc.Parameters.AddWithValue("id", docId);
                insDoc.Parameters.AddWithValue("t", world.TenantAId);
                insDoc.Parameters.AddWithValue("e", world.EntityA1Id);
                insDoc.Parameters.AddWithValue("num", docNum);
                insDoc.Parameters.AddWithValue("fn", $"evidence-{i:D2}.txt");
                insDoc.Parameters.AddWithValue("sz", (long)payload.Length);
                insDoc.Parameters.AddWithValue("sha", sha);
                insDoc.Parameters.AddWithValue("bytes", payload);
                insDoc.Parameters.AddWithValue("u", world.ControllerId);
                await insDoc.ExecuteNonQueryAsync();

                await using var insLink = new NpgsqlCommand("""
                    INSERT INTO documents.document_links
                        (link_id, tenant_id, legal_entity_id, document_id, source_module, source_document_type, source_document_id, source_document_number, created_by_user_id, created_at_utc)
                    VALUES
                        (@lid, @t, @e, @did, 'PROCUREMENT', 'PURCHASE_ORDER', @sid, @snum, @u, NOW());
                    """, appConn, tx);
                insLink.Parameters.AddWithValue("lid", Guid.NewGuid());
                insLink.Parameters.AddWithValue("t", world.TenantAId);
                insLink.Parameters.AddWithValue("e", world.EntityA1Id);
                insLink.Parameters.AddWithValue("did", docId);
                insLink.Parameters.AddWithValue("sid", $"PO-LOAD-{i:D2}");
                insLink.Parameters.AddWithValue("snum", $"PO-2026-LOAD-{i:D2}");
                insLink.Parameters.AddWithValue("u", world.ControllerId);
                await insLink.ExecuteNonQueryAsync();
            }

            await using (var qCmd = new NpgsqlCommand("""
                SELECT COUNT(*) FROM documents.business_documents d
                JOIN documents.document_links l
                  ON d.tenant_id = l.tenant_id AND d.legal_entity_id = l.legal_entity_id AND d.document_id = l.document_id
                WHERE d.legal_entity_id = @e AND l.source_document_type = 'PURCHASE_ORDER';
                """, appConn, tx))
            {
                qCmd.Parameters.AddWithValue("e", world.EntityA1Id);
                Assert.Equal(50L, Convert.ToInt64(await qCmd.ExecuteScalarAsync()));
            }

            await tx.CommitAsync();
        }
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000, $"50-document insert + join took {sw.ElapsedMilliseconds}ms");

        // Tenant B sees 0 rows
        await using (var txB = await appConn.BeginTransactionAsync())
        {
            await using (var setB = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, true);", appConn, txB))
            {
                setB.Parameters.AddWithValue("t", world.TenantBId.ToString("D"));
                await setB.ExecuteNonQueryAsync();
            }
            await using (var cntB = new NpgsqlCommand("SELECT COUNT(*) FROM documents.business_documents;", appConn, txB))
            {
                Assert.Equal(0L, Convert.ToInt64(await cntB.ExecuteScalarAsync()));
            }
            await txB.RollbackAsync();
        }

        // Immutability trigger blocks UPDATE even as migrator when tenant context is set
        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using var txMig = await migConn.BeginTransactionAsync();
        await using (var setMig = new NpgsqlCommand("SELECT set_config('app.tenant_id', @t, true);", migConn, txMig))
        {
            setMig.Parameters.AddWithValue("t", world.TenantAId.ToString("D"));
            await setMig.ExecuteNonQueryAsync();
        }
        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var upd = new NpgsqlCommand("UPDATE documents.business_documents SET file_name = 'tampered.txt' WHERE document_id = @id;", migConn, txMig);
            upd.Parameters.AddWithValue("id", sampleDocId);
            await upd.ExecuteNonQueryAsync();
        });
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        await txMig.RollbackAsync();
    }

    [Fact]
    public async Task S17_API_01_UploadSignedDownloadForgedIdBlockPdfSummaryAuditCsvAndOutboxRetry()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var ctrlClient = await SignInAndGetCsrfAsync(factory, world.ControllerEmail, world.Password);
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignEmail, world.Password);

        var basePath = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/documents";

        // 1. Reject unsafe MZ executable disguised as PDF (422)
        var mzPayload = Convert.ToBase64String(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 });
        var badRes = await ctrlClient.PostAsJsonAsync($"{basePath}/upload", new UploadBusinessDocumentRequest(
            "malware.pdf", "application/pdf", mzPayload, "GENERAL",
            "PROCUREMENT", "PURCHASE_ORDER", "po-test-01", "PO-2026-00001", null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badRes.StatusCode);

        // 2. Upload valid PDF evidence (201)
        var rawPdf = DocumentSecurity.BuildSimplePdf("Faktur Pajak Bukti S17", ["010.000-26.00000123", "Total: Rp 111.000"]);
        var expectedSha = DocumentSecurity.ComputeSha256Hex(rawPdf);
        var sourceId = "INV-S17-001";
        var upRes = await ctrlClient.PostAsJsonAsync($"{basePath}/upload", new UploadBusinessDocumentRequest(
            "faktur-pajak-s17.pdf",
            "application/pdf",
            Convert.ToBase64String(rawPdf),
            "TAX_EVIDENCE",
            "BILLING",
            "CUSTOMER_INVOICE",
            sourceId,
            "INV-2026-S17",
            "Bukti Faktur Pajak S17"));
        Assert.Equal(HttpStatusCode.Created, upRes.StatusCode);
        var uploaded = await upRes.Content.ReadFromJsonAsync<BusinessDocumentItemDto>(JsonOptions);
        Assert.NotNull(uploaded);
        Assert.StartsWith("DOC-", uploaded!.DocumentNumber, StringComparison.Ordinal);
        Assert.Equal(expectedSha, uploaded.Sha256Hash);

        // 3. Issue signed download token & verify exact downloaded bytes
        var tokRes = await ctrlClient.GetAsync($"{basePath}/{uploaded.DocumentId}/download-token");
        Assert.Equal(HttpStatusCode.OK, tokRes.StatusCode);
        var tok = await tokRes.Content.ReadFromJsonAsync<DocumentDownloadTokenResponse>(JsonOptions);
        Assert.NotNull(tok);

        var dlRes = await ctrlClient.GetAsync(tok!.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, dlRes.StatusCode);
        Assert.Equal("nosniff", dlRes.Headers.GetValues("X-Content-Type-Options").Single());
        var downloadedBytes = await dlRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(expectedSha, DocumentSecurity.ComputeSha256Hex(downloadedBytes));

        // 4. Tampered token & cross-tenant forged document ID rejected
        var forgedTokRes = await ctrlClient.GetAsync($"{basePath}/{uploaded.DocumentId}/download?token=9999999999.deadbeef");
        Assert.Equal(HttpStatusCode.Forbidden, forgedTokRes.StatusCode);

        var foreignTokRes = await foreignClient.GetAsync($"{basePath}/{uploaded.DocumentId}/download-token");
        Assert.Equal(HttpStatusCode.NotFound, foreignTokRes.StatusCode);

        var crossTenantRes = await ctrlClient.GetAsync(
            $"/api/workspaces/{world.TenantBId}/entities/{world.EntityB1Id}/documents/{uploaded.DocumentId}/download-token");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantRes.StatusCode);

        // 5. Evidence pack PDF generation check
        var pdfRes = await ctrlClient.GetAsync($"{basePath}/evidence-pack/CUSTOMER_INVOICE/{sourceId}/summary.pdf");
        Assert.Equal(HttpStatusCode.OK, pdfRes.StatusCode);
        var pdfText = Encoding.ASCII.GetString(await pdfRes.Content.ReadAsByteArrayAsync());
        Assert.StartsWith("%PDF-1.4", pdfText, StringComparison.Ordinal);
        Assert.Contains(uploaded.DocumentNumber, pdfText, StringComparison.Ordinal);
        Assert.Contains(expectedSha, pdfText, StringComparison.Ordinal);

        // 6. Entity-scoped Audit CSV export
        var csvRes = await ctrlClient.GetAsync($"{basePath}/audit-export.csv");
        Assert.Equal(HttpStatusCode.OK, csvRes.StatusCode);
        var csvText = await csvRes.Content.ReadAsStringAsync();
        Assert.Contains("OccurredAtUtc,EventType,SubjectType,SubjectId,Summary", csvText, StringComparison.Ordinal);
        Assert.Contains("DOCUMENT_EVIDENCE_ATTACHED", csvText, StringComparison.Ordinal);

        // 7. Insert a FAILED outbox message for Entity A1 and retry via API
        var failedMsgId = Guid.NewGuid();
        await using (var migConn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await migConn.OpenAsync();
            await using var insOut = new NpgsqlCommand("""
                INSERT INTO integration.outbox_messages
                    (message_id, tenant_id, legal_entity_id, topic, source_key, payload_json, status, attempt_count, last_error, next_attempt_at_utc, created_at_utc, version)
                VALUES
                    (@id, @t, @e, 's17.test.topic', @sk, '{"test":true}'::jsonb, 'FAILED', 2, 'Simulated transient timeout', NOW(), NOW(), 1);
                """, migConn);
            insOut.Parameters.AddWithValue("id", failedMsgId);
            insOut.Parameters.AddWithValue("t", world.TenantAId);
            insOut.Parameters.AddWithValue("e", world.EntityA1Id);
            insOut.Parameters.AddWithValue("sk", failedMsgId.ToString("D"));
            await insOut.ExecuteNonQueryAsync();
        }

        var retryRes = await ctrlClient.PostAsync($"{basePath}/outbox/{failedMsgId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retryRes.StatusCode);
        var retryDto = await retryRes.Content.ReadFromJsonAsync<OutboxRetryResponse>(JsonOptions);
        Assert.Equal("PENDING", retryDto!.Status);

        // 8. Read-only Entity A2 upload attempt denied (403)
        var sbyRes = await ctrlClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/documents/upload",
            new UploadBusinessDocumentRequest(
                "sby.pdf", "application/pdf", Convert.ToBase64String(rawPdf), "GENERAL",
                "BILLING", "CUSTOMER_INVOICE", "inv-sby", "INV-SBY-1", null));
        Assert.Equal(HttpStatusCode.Forbidden, sbyRes.StatusCode);
    }

    private static async Task<S17World> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        var now = DateTimeOffset.UtcNow;
        const string password = "AtheraTest!2026";
        var (ctrlId, ctrlEmail, _) = await fx.CreateTestUserAsync($"ctrl-{Guid.NewGuid():N}@test.athera.local", "Budi Controller", password);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync($"foreign-{Guid.NewGuid():N}@test.athera.local", "Foreign User", password);

        var tenantA = Guid.NewGuid();
        var entityA1 = Guid.NewGuid();
        var entityA2 = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityB1 = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.Add(Tenant.Create(tenantA, "NUSA", "PT Nusantara Test", now));
        db.Tenants.Add(Tenant.Create(tenantB, "SMDR", "PT Samudra Test", now));
        db.LegalEntities.Add(LegalEntity.Create(entityA1, tenantA, "NUSA-JKT", "PT Nusantara Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now));
        db.LegalEntities.Add(LegalEntity.Create(entityA2, tenantA, "NUSA-SBY", "PT Nusantara Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now));
        db.LegalEntities.Add(LegalEntity.Create(entityB1, tenantB, "SMDR-SBY", "PT Samudra Surabaya", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), tenantA, ctrlId, now));
        db.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), tenantB, foreignId, now));
        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost, PermissionCodes.AdminManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantA, entityA1, ctrlId, perm, now, null, now));
        }
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantA, entityA2, ctrlId, PermissionCodes.FinanceRead, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantB, entityB1, foreignId, PermissionCodes.FinanceRead, now, null, now));
        await db.SaveChangesAsync();

        return new S17World(tenantA, entityA1, entityA2, tenantB, entityB1, ctrlId, ctrlEmail, foreignId, foreignEmail, password);
    }

    private static async Task<HttpClient> SignInAndGetCsrfAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var csrfRes = await client.GetAsync("/api/auth/csrf");
        csrfRes.EnsureSuccessStatusCode();
        var csrfPayload = await csrfRes.Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);

        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, password, false))
        };
        loginReq.Headers.Add(csrfPayload!.HeaderName, csrfPayload.RequestToken);
        var loginRes = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session!.CsrfToken);
        return client;
    }
}
