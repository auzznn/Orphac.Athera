using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S14BillingPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S14SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid SalesOfficerId,
        string SalesOfficerEmail,
        Guid FinanceControllerId,
        string FinanceControllerEmail,
        Guid ProcurementOnlyId,
        string ProcurementOnlyEmail,
        Guid ForeignUserB1Id,
        string ForeignUserB1Email,
        Guid PpnOut11Id,
        Guid PpnOutExemptId,
        Guid ApprovedSoId,
        Guid DispatchedShipmentId,
        Guid DispatchedShipmentLineId,
        string Password);

    [Fact]
    public async Task S14_PG_01_ForcedRlsCrossTenantIsolationAndPostedInvoiceImmutabilityTriggers()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS14WorldAsync(fx);

        // Verify ENABLE + FORCE ROW LEVEL SECURITY on all 6 billing/ar tables
        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        await using (var rlsCmd = appConn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE ((n.nspname = 'billing' AND c.relname IN ('invoice_sequences', 'credit_note_sequences', 'customer_invoices', 'customer_invoice_lines'))
                    OR (n.nspname = 'ar' AND c.relname IN ('receivable_sequences', 'customer_receivables')))
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(6L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Create and post one invoice in Tenant A via coordinator
        Guid postedInvoiceId;
        Guid postedLineId;
        await using (var db = fx.CreateMigratorDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var draftRes = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(
                db,
                new CreateCustomerInvoiceCommand(
                    world.TenantAId,
                    world.EntityA1Id,
                    world.ApprovedSoId,
                    world.PpnOut11Id,
                    new DateOnly(2026, 11, 19),
                    new DateOnly(2026, 12, 19),
                    "FP-010.001-26.00000001",
                    "s14-pg01-inv-1",
                    "Initial invoice",
                    [new BillingLineInput(world.DispatchedShipmentLineId, 2m)],
                    world.FinanceControllerId,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(draftRes.Succeeded);

            var postRes = await BillingAndReceivablesCoordinator.PostInvoiceAsync(
                db,
                new PostCustomerInvoiceCommand(
                    world.TenantAId,
                    world.EntityA1Id,
                    draftRes.Value.Invoice.InvoiceId,
                    null,
                    world.FinanceControllerId,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(postRes.Succeeded);
            postedInvoiceId = postRes.Value!.Invoice.InvoiceId;
            postedLineId = postRes.Value.Lines[0].InvoiceLineId;
            await tx.CommitAsync();
        }

        // 1. Missing tenant GUC sees 0 rows; Tenant B sees 0 rows
        await using (var cmdNoCtx = new NpgsqlCommand("SELECT COUNT(*) FROM billing.customer_invoices", appConn))
        {
            Assert.Equal(0L, (long)(await cmdNoCtx.ExecuteScalarAsync())!);
        }

        await using (var txB = await appConn.BeginTransactionAsync())
        {
            await using (var setB = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txB))
            {
                setB.Parameters.AddWithValue("tid", world.TenantBId.ToString("D"));
                await setB.ExecuteNonQueryAsync();
            }

            await using (var countInvB = new NpgsqlCommand("SELECT COUNT(*) FROM billing.customer_invoices", appConn, txB))
            {
                Assert.Equal(0L, (long)(await countInvB.ExecuteScalarAsync())!);
            }

            await using (var countArB = new NpgsqlCommand("SELECT COUNT(*) FROM ar.customer_receivables", appConn, txB))
            {
                Assert.Equal(0L, (long)(await countArB.ExecuteScalarAsync())!);
            }

            await txB.RollbackAsync();
        }

        // 2. Under Tenant A as athera_app:
        //    - Updating POSTED customer_invoices fails with SQLSTATE 55000 (immutability trigger)
        //    - Updating customer_invoice_lines fails with SQLSTATE 42501 (athera_app lacks UPDATE privilege)
        await using (var txA = await appConn.BeginTransactionAsync())
        {
            await using (var setA = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txA))
            {
                setA.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
                await setA.ExecuteNonQueryAsync();
            }

            var pgExInv = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand(
                    "UPDATE billing.customer_invoices SET notes = 'tampered' WHERE invoice_id = @id", appConn, txA);
                upd.Parameters.AddWithValue("id", postedInvoiceId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("55000", pgExInv.SqlState);
            await txA.RollbackAsync();
        }

        await using (var txALine = await appConn.BeginTransactionAsync())
        {
            await using (var setA = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txALine))
            {
                setA.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
                await setA.ExecuteNonQueryAsync();
            }

            var pgExLine = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand(
                    "UPDATE billing.customer_invoice_lines SET billed_base_quantity = 99 WHERE invoice_line_id = @id", appConn, txALine);
                upd.Parameters.AddWithValue("id", postedLineId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("42501", pgExLine.SqlState);
            await txALine.RollbackAsync();
        }

        // 3. Even as athera_migrator, updating a posted invoice line is blocked by trigger (SQLSTATE 55000)
        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using (var txMig = await migConn.BeginTransactionAsync())
        {
            var pgExMigLine = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand(
                    "UPDATE billing.customer_invoice_lines SET billed_base_quantity = 99 WHERE invoice_line_id = @id", migConn, txMig);
                upd.Parameters.AddWithValue("id", postedLineId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("55000", pgExMigLine.SqlState);
            await txMig.RollbackAsync();
        }
    }

    [Fact]
    public async Task S14_PG_02_ConcurrentInvoicingOnSameShipmentSerializesAndPreventsDoubleBilling()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS14WorldAsync(fx);

        // Seeded shipment line has 4 PCS dispatched. Launch two concurrent drafts each requesting 3 PCS (total 6 > 4).
        // FOR UPDATE lock on fulfillment.shipment_lines must serialize them so exactly 1 succeeds and 1 fails with 409 billing_exceeds_shipped_capacity.
        var t1 = Task.Run(async () =>
        {
            await using var db = fx.CreateMigratorDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var res = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(
                db,
                new CreateCustomerInvoiceCommand(
                    world.TenantAId, world.EntityA1Id, world.ApprovedSoId, world.PpnOut11Id,
                    new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "FP-CONC-1", "conc-inv-1", null,
                    [new BillingLineInput(world.DispatchedShipmentLineId, 3m)],
                    world.SalesOfficerId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            if (res.Succeeded) await tx.CommitAsync();
            else await tx.RollbackAsync();
            return res;
        });

        var t2 = Task.Run(async () =>
        {
            await using var db = fx.CreateMigratorDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            var res = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(
                db,
                new CreateCustomerInvoiceCommand(
                    world.TenantAId, world.EntityA1Id, world.ApprovedSoId, world.PpnOut11Id,
                    new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "FP-CONC-2", "conc-inv-2", null,
                    [new BillingLineInput(world.DispatchedShipmentLineId, 3m)],
                    world.SalesOfficerId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            if (res.Succeeded) await tx.CommitAsync();
            else await tx.RollbackAsync();
            return res;
        });

        var results = await Task.WhenAll(t1, t2);
        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.Equal(1, results.Count(r => !r.Succeeded && r.StatusCode == 409 && r.ErrorCode == "billing_exceeds_shipped_capacity"));
    }

    [Fact]
    public async Task S14_API_01_PartialBillingTaxVariantsIdempotencyClosedPeriodAndAtomicRollback()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS14WorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var salesClient = await SignInAndGetCsrfAsync(factory, world.SalesOfficerEmail, world.Password);
        using var financeClient = await SignInAndGetCsrfAsync(factory, world.FinanceControllerEmail, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/billing";

        // 1. Sales drafts partial invoice (2 of 4 PCS) with PPN-OUT-11 (unit price 150,000 => subtotal 300,000, tax 33,000, grand 333,000)
        var draftReq = new CreateCustomerInvoiceRequest(
            world.ApprovedSoId,
            world.PpnOut11Id,
            new DateOnly(2026, 11, 19),
            new DateOnly(2026, 12, 19),
            null,
            "api-inv-part-1",
            "Partial billing 2 PCS",
            [new CreateCustomerInvoiceLineRequest(world.DispatchedShipmentLineId, 2m)]);

        var draftResp = await salesClient.PostAsJsonAsync($"{baseUrl}/invoices", draftReq);
        Assert.Equal(HttpStatusCode.Created, draftResp.StatusCode);
        var inv1 = (await draftResp.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal("DRAFT", inv1.Status);
        Assert.Equal(300_000m, inv1.SubtotalAmount);
        Assert.Equal(33_000m, inv1.TaxAmount);
        Assert.Equal(333_000m, inv1.GrandTotalAmount);

        // 2. Duplicate idempotent submit returns same invoice without double billing
        var replayResp = await salesClient.PostAsJsonAsync($"{baseUrl}/invoices", draftReq);
        Assert.Equal(HttpStatusCode.OK, replayResp.StatusCode);
        var replayInv = (await replayResp.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal(inv1.InvoiceId, replayInv.InvoiceId);

        // 3. Sales submits draft -> SUBMITTED, and cannot post (403)
        var submitResp = await salesClient.PostAsync($"{baseUrl}/invoices/{inv1.InvoiceId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        var salesPostDenied = await salesClient.PostAsJsonAsync($"{baseUrl}/invoices/{inv1.InvoiceId}/post", new PostCustomerInvoiceRequest("FP-010.001-26.00000010"));
        Assert.Equal(HttpStatusCode.Forbidden, salesPostDenied.StatusCode);

        // 4. Finance posting WITHOUT Faktur Pajak evidence fails (409 OD-07)
        var postNoEvidence = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{inv1.InvoiceId}/post", new PostCustomerInvoiceRequest(null));
        Assert.Equal(HttpStatusCode.Conflict, postNoEvidence.StatusCode);

        // 5. Finance posts WITH Faktur Pajak evidence -> 200 OK, Dr 1200 / Cr 4100 / Cr 2210 + AR Receivable created
        var postOk = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{inv1.InvoiceId}/post", new PostCustomerInvoiceRequest("FP-010.001-26.00000010"));
        Assert.Equal(HttpStatusCode.OK, postOk.StatusCode);
        var postedInv1 = (await postOk.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal("POSTED", postedInv1.Status);
        Assert.NotNull(postedInv1.GlJournalNumber);
        Assert.NotNull(postedInv1.ArReceivableId);

        // 6. Second partial invoice (remaining 2 PCS) using PPN-OUT-EXEMPT (0% tax) in CLOSED period (2026-10-15) -> post blocked with atomic rollback
        var exemptClosedDraft = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices", new CreateCustomerInvoiceRequest(
            world.ApprovedSoId,
            world.PpnOutExemptId,
            new DateOnly(2026, 10, 15),
            new DateOnly(2026, 11, 14),
            "SKB-PPN-2026-009",
            "api-inv-closed-1",
            "Closed period test",
            [new CreateCustomerInvoiceLineRequest(world.DispatchedShipmentLineId, 2m)]));
        Assert.Equal(HttpStatusCode.Created, exemptClosedDraft.StatusCode);
        var closedInv = (await exemptClosedDraft.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal(0m, closedInv.TaxAmount);
        Assert.Equal(300_000m, closedInv.GrandTotalAmount);

        var postClosed = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{closedInv.InvoiceId}/post", new PostCustomerInvoiceRequest("SKB-PPN-2026-009"));
        Assert.Equal(HttpStatusCode.Conflict, postClosed.StatusCode);

        // Verify rollback: closedInv is still DRAFT and only 1 AR receivable exists
        var overview = (await financeClient.GetFromJsonAsync<BillingOverviewResponse>($"{baseUrl}/overview", JsonOptions))!;
        Assert.Single(overview.Receivables);
        Assert.Equal(333_000m, overview.Receivables[0].OpenAmount);
        Assert.Equal("DRAFT", overview.Invoices.Single(i => i.InvoiceId == closedInv.InvoiceId).Status);

        // Cancel closed-period draft -> releases 2 PCS capacity -> re-bill with PPN-OUT-EXEMPT in OPEN period (2026-11-19)
        var cancelResp = await financeClient.PostAsync($"{baseUrl}/invoices/{closedInv.InvoiceId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        var exemptOpenDraft = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices", new CreateCustomerInvoiceRequest(
            world.ApprovedSoId,
            world.PpnOutExemptId,
            new DateOnly(2026, 11, 19),
            new DateOnly(2026, 12, 19),
            null,
            "api-inv-exempt-open",
            "Exempt open period",
            [new CreateCustomerInvoiceLineRequest(world.DispatchedShipmentLineId, 2m)]));
        Assert.Equal(HttpStatusCode.Created, exemptOpenDraft.StatusCode);
        var exemptInv = (await exemptOpenDraft.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;

        // Missing facility evidence blocked even at 0% (OD-07)
        var exemptPostBlocked = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{exemptInv.InvoiceId}/post", new PostCustomerInvoiceRequest(null));
        Assert.Equal(HttpStatusCode.Conflict, exemptPostBlocked.StatusCode);

        // With SKB facility evidence succeeds
        var exemptPostOk = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{exemptInv.InvoiceId}/post", new PostCustomerInvoiceRequest("SKB-PPN-2026-009"));
        Assert.Equal(HttpStatusCode.OK, exemptPostOk.StatusCode);

        var finalOverview = (await financeClient.GetFromJsonAsync<BillingOverviewResponse>($"{baseUrl}/overview", JsonOptions))!;
        Assert.Empty(finalOverview.BillableShipmentLines);
        Assert.Equal(2, finalOverview.Receivables.Count);
        Assert.Equal(633_000m, finalOverview.Stats.OpenReceivablesTotal);
    }

    [Fact]
    public async Task S14_API_02_CreditNoteCompensatesGlAndArAndEnforcesPermissionsAndCrossTenantDenial()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS14WorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var financeClient = await SignInAndGetCsrfAsync(factory, world.FinanceControllerEmail, world.Password);
        using var procClient = await SignInAndGetCsrfAsync(factory, world.ProcurementOnlyEmail, world.Password);
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignUserB1Email, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/billing";

        // 1. Permission boundary: Procurement-only user gets 403; Foreign tenant user gets 404; Finance on Entity A2 (read-only) gets CanDraft=false, CanPost=false
        var procDenied = await procClient.GetAsync($"{baseUrl}/overview");
        Assert.Equal(HttpStatusCode.Forbidden, procDenied.StatusCode);

        var foreignDenied = await foreignClient.GetAsync($"{baseUrl}/overview");
        Assert.Equal(HttpStatusCode.NotFound, foreignDenied.StatusCode);

        var sbyOverview = (await financeClient.GetFromJsonAsync<BillingOverviewResponse>(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/billing/overview", JsonOptions))!;
        Assert.False(sbyOverview.CanDraft);
        Assert.False(sbyOverview.CanPost);

        // 2. Post a 4 PCS invoice (subtotal 600,000, tax 66,000, grand 666,000)
        var draftResp = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices", new CreateCustomerInvoiceRequest(
            world.ApprovedSoId, world.PpnOut11Id, new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19),
            "FP-010.001-26.00000099", "cn-test-inv-1", null,
            [new CreateCustomerInvoiceLineRequest(world.DispatchedShipmentLineId, 4m)]));
        var inv = (await draftResp.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        var postResp = await financeClient.PostAsJsonAsync($"{baseUrl}/invoices/{inv.InvoiceId}/post", new PostCustomerInvoiceRequest(null));
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);

        // 3. Credit note WITHOUT Nota Retur evidence fails (409)
        var cnNoEvidence = await financeClient.PostAsJsonAsync($"{baseUrl}/credit-notes", new CreateCreditNoteRequest(
            inv.InvoiceId, new DateOnly(2026, 11, 19), null, "cn-no-ev", "Missing Nota Retur",
            [new CreateCreditNoteLineRequest(inv.Lines[0].InvoiceLineId, 1m)]));
        Assert.Equal(HttpStatusCode.Conflict, cnNoEvidence.StatusCode);

        // 4. Credit note for 1 PCS WITH Nota Retur succeeds -> reduces AR open balance by 166,500 (from 666,000 to 499,500)
        var cnOk = await financeClient.PostAsJsonAsync($"{baseUrl}/credit-notes", new CreateCreditNoteRequest(
            inv.InvoiceId, new DateOnly(2026, 11, 19), "NR-2026-0001", "cn-ok-1", "Price/quantity credit",
            [new CreateCreditNoteLineRequest(inv.Lines[0].InvoiceLineId, 1m)]));
        Assert.Equal(HttpStatusCode.Created, cnOk.StatusCode);
        var cnDto = (await cnOk.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal("CREDIT_NOTE", cnDto.DocumentType);
        Assert.Equal("POSTED", cnDto.Status);
        Assert.Equal(150_000m, cnDto.SubtotalAmount);
        Assert.Equal(16_500m, cnDto.TaxAmount);
        Assert.Equal(166_500m, cnDto.GrandTotalAmount);
        Assert.NotNull(cnDto.GlJournalNumber);

        // 5. Over-crediting (requesting 4 more PCS when only 3 remain) is rejected (409)
        var cnOver = await financeClient.PostAsJsonAsync($"{baseUrl}/credit-notes", new CreateCreditNoteRequest(
            inv.InvoiceId, new DateOnly(2026, 11, 19), "NR-2026-0002", "cn-over-1", "Over credit",
            [new CreateCreditNoteLineRequest(inv.Lines[0].InvoiceLineId, 4m)]));
        Assert.Equal(HttpStatusCode.Conflict, cnOver.StatusCode);

        var overviewAfter = (await financeClient.GetFromJsonAsync<BillingOverviewResponse>($"{baseUrl}/overview", JsonOptions))!;
        Assert.Single(overviewAfter.Receivables);
        Assert.Equal(666_000m, overviewAfter.Receivables[0].OriginalAmount);
        Assert.Equal(166_500m, overviewAfter.Receivables[0].CreditedAmount);
        Assert.Equal(499_500m, overviewAfter.Receivables[0].OpenAmount);
        Assert.Equal("PARTIALLY_SETTLED", overviewAfter.Receivables[0].Status);
        Assert.Equal(450_000m, overviewAfter.Stats.PostedRevenueNetTotal);
    }

    private static async Task<HttpClient> SignInAndGetCsrfAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:3200");
        var csrfPayload = await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest(email, password, false)) };
        loginReq.Headers.Add(csrfPayload!.HeaderName, csrfPayload.RequestToken);
        var loginRes = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session!.CsrfToken);
        return client;
    }

    private static async Task<S14SeedWorld> SeedS14WorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (salesId, salesEmail, _) = await fx.CreateTestUserAsync("sales.a1.s14@test.local", "Nadia Sales", pw);
        var (financeId, financeEmail, _) = await fx.CreateTestUserAsync("fin.a1.s14@test.local", "Budi Finance", pw);
        var (procId, procEmail, _) = await fx.CreateTestUserAsync("proc.a1.s14@test.local", "Rina Proc", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("for.b1.s14@test.local", "Samudra Staff", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-30);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", now),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", now));

        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, salesId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, financeId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, procId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.SalesRead, PermissionCodes.SalesManage, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, salesId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceJournalPost, PermissionCodes.SalesRead, PermissionCodes.SalesApprove, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, financeId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.SalesRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, financeId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, procId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceJournalPost })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, p, now, null, now));
        }

        db.ApprovalPolicies.Add(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now));

        var arAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1200", "Piutang Usaha", GlAccountTypes.Asset, GlAccountCategories.AccountsReceivable, null, true, true, now);
        var invAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnOutAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2210", "PPN Keluaran", GlAccountTypes.Liability, GlAccountCategories.TaxPayable, null, true, false, now);
        var revAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "4100", "Pendapatan Penjualan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, null, true, false, now);
        var cogsAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "HPP", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, false, now);
        db.GlAccounts.AddRange(arAcct, invAcct, ppnOutAcct, revAcct, cogsAcct);

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ArTradeControl, arAcct, "AR 1200", financeId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcct, "Inv 1300", financeId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnOutputTax, ppnOutAcct, "PPN Out 2210", financeId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.SalesRevenueDefault, revAcct, "Sales Rev 4100", financeId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcct, "COGS 5100", financeId, now));

        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        var ppnOut11 = TaxCode.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "PPN-OUT-11", "PPN Keluaran 11%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable,
            11m, 1, 1, true, false, ppnOutAcct.AccountId, new DateOnly(2025, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "OD-07", now);
        var ppnOutExempt = TaxCode.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "PPN-OUT-EXEMPT", "PPN Dibebaskan 0%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.Exempt,
            0m, 1, 1, true, false, null, new DateOnly(2025, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "OD-07", now);
        db.TaxCodes.AddRange(ppnOut11, ppnOutExempt);

        var cust = Partner.Create(Guid.NewGuid(), tenantAId, "PT-CUST-001", "PT Graha Ritel Nusantara", "Graha Ritel", false, true, "01.777.332.1-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var itemValve = Item.Create(Guid.NewGuid(), tenantAId, "ITM-VALVE-01", "Katup Industri PN16", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Partners.Add(cust);
        db.UnitsOfMeasure.Add(pcs);
        db.Items.Add(itemValve);
        await db.SaveChangesAsync();

        var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);
        var locMain = layout.LocationsByCode[InventoryAvailabilityDefaults.DefaultMainLocationCode];

        var pos = StockPosition.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, pcs.UomId, now);
        pos.ApplyInboundReceipt(20m, StockStatuses.Unrestricted, now);
        var pool = ValuationPool.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, "IDR", pcs.UomId, now);
        pool.ApplyReceiptValuation(20m, 1_000_000m, now); // Rp 50.000/PCS
        var bal = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, pos.StockPositionId, itemValve.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locMain.LocationId, null, StockStatuses.Unrestricted, now);
        bal.AdjustOnHand(20m, now);
        db.StockPositions.Add(pos);
        db.ValuationPools.Add(pool);
        db.StockLocationBalances.Add(bal);
        await db.SaveChangesAsync();

        // Create & approve SO (10 PCS @ Rp 150.000/PCS) via S12 SalesOrderCoordinator
        Guid soId;
        Guid soLineId;
        await using (var txSo = await db.Database.BeginTransactionAsync())
        {
            var draft = await SalesOrderCoordinator.CreateDraftAsync(
                db,
                new CreateSoDraftCommand(
                    tenantAId, entityA1Id, "IDR", "IDR", cust.PartnerId,
                    new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25),
                    "Gudang Bekasi Timur", 30, "PO-CUST-S14", false, null,
                    [new SoLineInput("SO-L001", itemValve.ItemId, pcs.UomId, 10m, 150_000m, 0m, ppnOut11.TaxCodeId, null, null)],
                    salesId, now),
                CancellationToken.None);
            Assert.True(draft.Succeeded);

            var sub = await SalesOrderCoordinator.SubmitRevisionAsync(
                db,
                new SubmitSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder!.SalesOrderId, draft.Revision!.RevisionId, salesId, now.AddMinutes(1)),
                CancellationToken.None);
            Assert.True(sub.Succeeded);

            var dec = await SalesOrderCoordinator.DecideRevisionAsync(
                db,
                new DecideSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder.SalesOrderId, draft.Revision.RevisionId, ApprovalDecisions.Approved, "Approved for S14", null, financeId, now.AddMinutes(2)),
                CancellationToken.None);
            Assert.True(dec.Succeeded);
            await txSo.CommitAsync();

            soId = draft.SalesOrder.SalesOrderId;
            soLineId = await db.SalesOrderLines.AsNoTracking()
                .Where(l => l.TenantId == tenantAId && l.LegalEntityId == entityA1Id && l.RevisionId == draft.Revision.RevisionId)
                .Select(l => l.SoLineId)
                .SingleAsync();
        }

        // Reserve 4, Pick 4, Dispatch 4 via S13 SalesFulfillmentCoordinator
        Guid shpId;
        Guid shpLineId;
        await using (var txFul = await db.Database.BeginTransactionAsync())
        {
            var rsv = await SalesFulfillmentCoordinator.ReserveSalesOrderLineAsync(
                db,
                new ReserveSalesOrderLineCommand(tenantAId, entityA1Id, soId, soLineId, locMain.LocationId, null, 4m, "s14-seed-rsv", null, salesId, now),
                CancellationToken.None);
            Assert.True(rsv.Succeeded);

            var pck = await SalesFulfillmentCoordinator.CreatePickOrderAsync(
                db,
                new CreatePickOrderCommand(tenantAId, entityA1Id, soId, true, "s14-seed-pck", null, [new PickLineInput(rsv.Reservation!.StockReservationId, 4m, 4m)], salesId, now),
                CancellationToken.None);
            Assert.True(pck.Succeeded);

            var shp = await SalesFulfillmentCoordinator.DispatchShipmentAsync(
                db,
                new DispatchSalesShipmentCommand(
                    tenantAId, entityA1Id, "IDR", pck.PickOrder!.PickOrderId,
                    new DateOnly(2026, 11, 19), "SJ-S14-SEED-001", "Internal Fleet", "B 1234 XY", "s14-seed-shp", null,
                    null, salesId, now),
                CancellationToken.None);
            Assert.True(shp.Succeeded);
            await txFul.CommitAsync();

            shpId = shp.Shipment!.ShipmentId;
            shpLineId = shp.Lines[0].ShipmentLineId;
        }

        return new S14SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            salesId, salesEmail, financeId, financeEmail, procId, procEmail, foreignId, foreignEmail,
            ppnOut11.TaxCodeId, ppnOutExempt.TaxCodeId, soId, shpId, shpLineId, pw);
    }
}
