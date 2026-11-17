using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S05ProcurementPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S05SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid BuyerA1Id,
        string BuyerA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid DualRoleA1Id,
        string DualRoleA1Email,
        Guid ReaderA2Id,
        string ReaderA2Email,
        Guid MemberB1NoProcId,
        string MemberB1NoProcEmail,
        Guid ForeignManagerB1Id,
        string ForeignManagerB1Email,
        Guid SupplierAllAId,
        Guid SupplierRestrictedA1OnlyId,
        Guid ForeignSupplierBId,
        Guid PcsUomAId,
        Guid BoxUomAId,
        Guid ItemStockedAId,
        Guid ItemServiceAId,
        Guid ItemSalesOnlyAId,
        Guid ForeignItemBId,
        Guid ForeignUomBId,
        Guid PpnInTaxA1Id,
        Guid ToleranceA1Id,
        Guid ForeignToleranceB1Id,
        string Password);

    [Fact]
    public async Task S05_PG_01_MissingContextDeniesAndCrossTenantProcurementIsolatedUnderForcedRls()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'approval' AND c.relname = 'document_approvals')
                    OR (n.nspname = 'procurement' AND c.relname IN ('delivery_tolerance_policies', 'po_sequences', 'purchase_orders', 'purchase_order_revisions', 'purchase_order_lines'))
                )
                AND c.relrowsecurity
                AND c.relforcerowsecurity;
                """;
            Assert.Equal(6L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        await using (var zeroRead = conn.CreateCommand())
        {
            zeroRead.CommandText = "SELECT count(*) FROM procurement.delivery_tolerance_policies";
            Assert.Equal(0L, Convert.ToInt64(await zeroRead.ExecuteScalarAsync()));
        }

        await using (var writeNoCtx = conn.CreateCommand())
        {
            writeNoCtx.CommandText = """
                INSERT INTO procurement.delivery_tolerance_policies (
                    policy_id, tenant_id, legal_entity_id, code, name, mode,
                    overdelivery_tolerance_percent, overdelivery_tolerance_base_qty, allow_unlimited_overdelivery,
                    review_status, is_default, status, created_at_utc, updated_at_utc, version)
                VALUES (@pid, @tid, @eid, 'ROGUE', 'Rogue', 'EXACT_APPROVED_QTY_ONLY', 0, 0, false, 'UNREVIEWED_SAFE_DEFAULT', false, 'ACTIVE', now(), now(), 1);
                """;
            writeNoCtx.Parameters.AddWithValue("pid", Guid.NewGuid());
            writeNoCtx.Parameters.AddWithValue("tid", world.TenantBId);
            writeNoCtx.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeNoCtx.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        await using (var readForeign = conn.CreateCommand())
        {
            readForeign.Transaction = tx;
            readForeign.CommandText = "SELECT count(*) FROM procurement.delivery_tolerance_policies WHERE policy_id = @fid;";
            readForeign.Parameters.AddWithValue("fid", world.ForeignToleranceB1Id);
            Assert.Equal(0L, Convert.ToInt64(await readForeign.ExecuteScalarAsync()));
        }

        await using (var writeForeign = conn.CreateCommand())
        {
            writeForeign.Transaction = tx;
            writeForeign.CommandText = """
                INSERT INTO procurement.delivery_tolerance_policies (
                    policy_id, tenant_id, legal_entity_id, code, name, mode,
                    overdelivery_tolerance_percent, overdelivery_tolerance_base_qty, allow_unlimited_overdelivery,
                    review_status, is_default, status, created_at_utc, updated_at_utc, version)
                VALUES (@pid, @tid, @eid, 'CROSS', 'Cross Tenant', 'EXACT_APPROVED_QTY_ONLY', 0, 0, false, 'UNREVIEWED_SAFE_DEFAULT', false, 'ACTIVE', now(), now(), 1);
                """;
            writeForeign.Parameters.AddWithValue("pid", Guid.NewGuid());
            writeForeign.Parameters.AddWithValue("tid", world.TenantBId);
            writeForeign.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeForeign.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task S05_PG_02_MigrationM05DownAndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117200508_M04_FinancialConfigAndPostingKernel");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema = 'procurement' OR (table_schema = 'approval' AND table_name = 'document_approvals');
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCheck = conn.CreateCommand();
            upCheck.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema = 'procurement' OR (table_schema = 'approval' AND table_name = 'document_approvals');
                """;
            Assert.Equal(6L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S05_PG_03_SqlDeleteRevokedFourEyesConstraintAndFrozenSnapshotTriggersBlockTampering()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        Guid poId;
        Guid rev1Id;
        Guid approvalId;

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            await using var tx = await migDb.Database.BeginTransactionAsync();
            var now = DateTimeOffset.UtcNow;
            var create = await PurchaseOrderWorkflowService.CreateDraftAsync(
                migDb,
                new CreatePoDraftCommand(
                    world.TenantAId, world.EntityA1Id, "IDR", "IDR",
                    world.SupplierAllAId, new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25),
                    "Gudang Jakarta", 30, world.ToleranceA1Id, "PG trigger test",
                    [new PoLineInput("L001", world.ItemStockedAId, world.BoxUomAId, 2m, 480_000m, world.PpnInTaxA1Id)],
                    world.BuyerA1Id, now),
                CancellationToken.None);
            Assert.True(create.Succeeded);
            poId = create.PurchaseOrder!.PurchaseOrderId;
            rev1Id = create.Revision!.RevisionId;

            var submit = await PurchaseOrderWorkflowService.SubmitRevisionAsync(
                migDb,
                new SubmitPoRevisionCommand(world.TenantAId, world.EntityA1Id, poId, rev1Id, null, world.BuyerA1Id, now.AddMinutes(1)),
                CancellationToken.None);
            Assert.True(submit.Succeeded);
            approvalId = submit.Revision!.ApprovalRecordId!.Value;
            await tx.CommitAsync();
        }

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var appTx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, appTx, world.TenantAId);

        // 1. DELETE revoked on document_approvals, purchase_orders, purchase_order_revisions (42501)
        foreach (var sql in new[]
        {
            ("approval.document_approvals", "approval_id", approvalId),
            ("procurement.purchase_order_revisions", "revision_id", rev1Id),
            ("procurement.purchase_orders", "purchase_order_id", poId)
        })
        {
            await appTx.SaveAsync("sp_del");
            await using var del = conn.CreateCommand();
            del.Transaction = appTx;
            del.CommandText = $"DELETE FROM {sql.Item1} WHERE {sql.Item2} = @id;";
            del.Parameters.AddWithValue("id", sql.Item3);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => del.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
            await appTx.RollbackAsync("sp_del");
        }

        // 2. Four-eyes CHECK constraint rejects decided_by_user_id == requested_by_user_id (23514)
        await appTx.SaveAsync("sp_self");
        await using (var selfCmd = conn.CreateCommand())
        {
            selfCmd.Transaction = appTx;
            selfCmd.CommandText = """
                UPDATE approval.document_approvals
                SET status = 'APPROVED', decided_by_user_id = requested_by_user_id, decided_at_utc = now(), decision_reason = 'Bypass'
                WHERE approval_id = @aid;
                """;
            selfCmd.Parameters.AddWithValue("aid", approvalId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => selfCmd.ExecuteNonQueryAsync());
            Assert.Equal("23514", ex.SqlState);
        }
        await appTx.RollbackAsync("sp_self");

        // 3. SQL Freeze Triggers block frozen revision snapshot update, unfreezing, line update, and line delete (55000)
        await appTx.SaveAsync("sp_snap");
        await using (var snapCmd = conn.CreateCommand())
        {
            snapCmd.Transaction = appTx;
            snapCmd.CommandText = "UPDATE procurement.purchase_order_revisions SET subtotal_amount = 1 WHERE revision_id = @rid;";
            snapCmd.Parameters.AddWithValue("rid", rev1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => snapCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("po_revision_frozen_immutable", ex.Message);
        }
        await appTx.RollbackAsync("sp_snap");

        await appTx.SaveAsync("sp_line_upd");
        await using (var lineUpdCmd = conn.CreateCommand())
        {
            lineUpdCmd.Transaction = appTx;
            lineUpdCmd.CommandText = "UPDATE procurement.purchase_order_lines SET unit_price = 1 WHERE revision_id = @rid;";
            lineUpdCmd.Parameters.AddWithValue("rid", rev1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => lineUpdCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("po_revision_frozen_immutable", ex.Message);
        }
        await appTx.RollbackAsync("sp_line_upd");

        await appTx.SaveAsync("sp_line_del");
        await using (var lineDelCmd = conn.CreateCommand())
        {
            lineDelCmd.Transaction = appTx;
            lineDelCmd.CommandText = "DELETE FROM procurement.purchase_order_lines WHERE revision_id = @rid;";
            lineDelCmd.Parameters.AddWithValue("rid", rev1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => lineDelCmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("po_revision_frozen_immutable", ex.Message);
        }
        await appTx.RollbackAsync("sp_line_del");
    }

    [Fact]
    public async Task S05_API_01_MultiLineDraftSubmitFourEyesSelfApproveBlockedApproveAndDuplicatePoNumber()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var dualActor = await CreateSignedInClientAsync(factory, world.DualRoleA1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";

        // 1. Create multi-line draft PO (2 BOX = 48 PCS + Service line)
        var createRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId,
            "IDR",
            new DateOnly(2026, 11, 18),
            new DateOnly(2026, 11, 28),
            "Gudang Utama Jakarta",
            30,
            world.ToleranceA1Id,
            "Pengadaan awal S05",
            [
                new PoLineInputDto(null, world.ItemStockedAId, world.BoxUomAId, 2m, 480_000m, world.PpnInTaxA1Id, LineDescription: "Karton"),
                new PoLineInputDto(null, world.ItemServiceAId, world.PcsUomAId, 1m, 250_000m, null, LineDescription: "Jasa kirim")
            ]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created!.PoNumber);
        Assert.Equal(PurchaseOrderStatuses.Draft, created.Status);
        var rev1 = Assert.Single(created.Revisions);
        Assert.Equal(49m, rev1.TotalOrderedBaseQuantity); // 48 PCS + 1 Service
        Assert.Equal(1_210_000m, rev1.SubtotalAmount); // 960k + 250k
        Assert.Equal(105_600m, rev1.PpnTaxTotalAmount); // 11% of 960k
        Assert.Equal(1_315_600m, rev1.GrandTotalAmount);

        // 2. Submit Rev 1 -> assigns PO-2026-00001 and freezes snapshot
        var submitRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{created.PurchaseOrderId:D}/revisions/{rev1.RevisionId:D}/submit", new SubmitPoRevisionRequest(null));
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);
        var submitted = await submitRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal("PO-2026-00001", submitted!.PoNumber);
        Assert.Equal(PurchaseOrderStatuses.PendingApproval, submitted.Status);
        Assert.True(submitted.Revisions[0].IsFrozen);

        // 3. Buyer without approve permission cannot approve (403)
        var buyerApproveAttempt = await buyer.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{created.PurchaseOrderId:D}/revisions/{rev1.RevisionId:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Attempt by requester"));
        Assert.Equal(HttpStatusCode.Forbidden, buyerApproveAttempt.StatusCode);

        // 4. Independent approver approves Rev 1 -> APPROVED
        var approveRes = await approver.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{created.PurchaseOrderId:D}/revisions/{rev1.RevisionId:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Disetujui empat mata"));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);
        var approved = await approveRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatuses.Approved, approved!.Status);
        Assert.Equal(rev1.RevisionId, approved.CurrentApprovedRevisionId);
        Assert.Equal(ReceivingProgressStatuses.NotReceived, approved.ReceivingProgress);
        Assert.Equal(49m, approved.RemainingOpenBaseQuantity);

        // 5. Dual-role actor creates & submits a second PO, then self-approval is still blocked (403 four_eyes_self_approval_forbidden)
        var create2Res = await dualActor.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang Barat", 14, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 10m, 20_000m, null)]));
        Assert.Equal(HttpStatusCode.Created, create2Res.StatusCode);
        var po2 = await create2Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);

        // Attempt submitting PO 2 with duplicate PoNumberOverride -> 409 duplicate_po_number
        var dupPoRes = await dualActor.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po2!.PurchaseOrderId:D}/revisions/{po2.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest("PO-2026-00001"));
        Assert.Equal(HttpStatusCode.Conflict, dupPoRes.StatusCode);
        var dupJson = await dupPoRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("duplicate_po_number", dupJson.GetProperty("errorCode").GetString());

        // Normal submit succeeds with PO-2026-00002
        var submit2Res = await dualActor.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po2.PurchaseOrderId:D}/revisions/{po2.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null));
        Assert.Equal(HttpStatusCode.OK, submit2Res.StatusCode);
        var po2Submitted = await submit2Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal("PO-2026-00002", po2Submitted!.PoNumber);

        // Same dual-role actor tries to approve own PO-2026-00002 -> 403 four_eyes_self_approval_forbidden
        var selfApproveRes = await dualActor.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po2.PurchaseOrderId:D}/revisions/{po2.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Trying to self-approve"));
        Assert.Equal(HttpStatusCode.Forbidden, selfApproveRes.StatusCode);
        var selfJson = await selfApproveRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("four_eyes_self_approval_forbidden", selfJson.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task S05_API_02_ConcurrentSixApprovalsAllowSingleDecision()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);

        var approvers = new List<HttpClient>();
        try
        {
            for (var i = 0; i < 6; i++)
            {
                approvers.Add(await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password));
            }

            var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";
            var createRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
                world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang Konkuren", 30, null, null,
                [new PoLineInputDto(null, world.ItemStockedAId, world.BoxUomAId, 1m, 480_000m, null)]));
            var created = await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
            var revId = created!.Revisions[0].RevisionId;

            var submitRes = await buyer.PostAsJsonAsync(
                $"{baseA1}/purchase-orders/{created.PurchaseOrderId:D}/revisions/{revId:D}/submit",
                new SubmitPoRevisionRequest(null));
            Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

            var tasks = approvers.Select(c => c.PostAsJsonAsync(
                $"{baseA1}/purchase-orders/{created.PurchaseOrderId:D}/revisions/{revId:D}/decide",
                new DecidePoRevisionRequest("APPROVED", "Concurrent check")));
            var responses = await Task.WhenAll(tasks);

            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

            await using var migDb = fx.CreateMigratorDbContext();
            Assert.Equal(1, await migDb.DocumentApprovals.CountAsync(a => a.TenantId == world.TenantAId && a.SourceRevisionId == revId && a.Status == DocumentApprovalStatuses.Approved));
            Assert.Equal(1, await migDb.OutboxMessages.CountAsync(o => o.TenantId == world.TenantAId && o.Topic == "procurement.po.approved.v1"));
        }
        finally
        {
            foreach (var c in approvers)
            {
                c.Dispose();
            }
        }
    }

    [Fact]
    public async Task S05_API_03_RevisionFreezePreservesOldSnapshotSupportsDiffAndRejectionRecovery()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";

        // 1. Draft & Approve Rev 1
        var createRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang Lama", 30, null, null,
            [new PoLineInputDto("L001", world.ItemStockedAId, world.BoxUomAId, 2m, 480_000m, world.PpnInTaxA1Id)]));
        var po = await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        var rev1Id = po!.Revisions[0].RevisionId;

        await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev1Id:D}/submit", new SubmitPoRevisionRequest(null));
        var appRev1Res = await approver.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev1Id:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Approve Rev 1"));
        Assert.Equal(HttpStatusCode.OK, appRev1Res.StatusCode);
        var approvedRev1Po = await appRev1Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        var rev1Hash = approvedRev1Po!.Revisions[0].SnapshotHash;
        var rev1Grand = approvedRev1Po.Revisions[0].GrandTotalAmount;

        // 2. Trying to edit frozen Rev 1 returns 409 po_revision_frozen_immutable
        var tamperRes = await buyer.PutAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev1Id:D}",
            new UpdatePurchaseOrderRevisionRequest(
                world.SupplierAllAId, new DateOnly(2026, 11, 18), null, "Tampered", 30, null, null, null,
                [new PoLineInputDto("L001", world.ItemStockedAId, world.BoxUomAId, 5m, 100_000m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, tamperRes.StatusCode);

        // 3. Create Amendment Rev 2 -> Reject it -> PO stays APPROVED on Rev 1
        var amend2Res = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions", new CreatePoAmendmentRequest("Naik jadi 3 box"));
        Assert.Equal(HttpStatusCode.Created, amend2Res.StatusCode);
        var rev2Id = (await amend2Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!.Revisions.First(r => r.RevisionNumber == 2).RevisionId;

        await buyer.PutAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2Id:D}",
            new UpdatePurchaseOrderRevisionRequest(
                world.SupplierAllAId, new DateOnly(2026, 11, 18), null, "Gudang Baru", 45, null, "Naik jadi 3 box", null,
                [new PoLineInputDto("L001", world.ItemStockedAId, world.BoxUomAId, 3m, 500_000m, world.PpnInTaxA1Id)]));
        await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2Id:D}/submit", new SubmitPoRevisionRequest(null));

        var rejectRev2Res = await approver.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2Id:D}/decide",
            new DecidePoRevisionRequest("REJECTED", "Harga 500k terlalu tinggi"));
        Assert.Equal(HttpStatusCode.OK, rejectRev2Res.StatusCode);
        var afterReject = await rejectRev2Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatuses.Approved, afterReject!.Status);
        Assert.Equal(rev1Id, afterReject.CurrentApprovedRevisionId);

        // 4. Create Amendment Rev 3 (priced 490k + add service line), inspect diff vs Rev 1, submit & approve
        var amend3Res = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions", new CreatePoAmendmentRequest("Negosiasi 490k + jasa kirim"));
        Assert.Equal(HttpStatusCode.Created, amend3Res.StatusCode);
        var rev3Id = (await amend3Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!.Revisions.First(r => r.RevisionNumber == 3).RevisionId;

        var upd3Res = await buyer.PutAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev3Id:D}",
            new UpdatePurchaseOrderRevisionRequest(
                world.SupplierAllAId, new DateOnly(2026, 11, 18), null, "Gudang Pusat Jakarta", 30, null,
                "Negosiasi 490k + jasa kirim", null,
                [
                    new PoLineInputDto("L001", world.ItemStockedAId, world.BoxUomAId, 3m, 490_000m, world.PpnInTaxA1Id),
                    new PoLineInputDto("L002", world.ItemServiceAId, world.PcsUomAId, 1m, 100_000m, null)
                ]));
        Assert.Equal(HttpStatusCode.OK, upd3Res.StatusCode);

        await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev3Id:D}/submit", new SubmitPoRevisionRequest(null));
        var app3Res = await approver.PostAsJsonAsync(
            $"{baseA1}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev3Id:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Harga revisi disetujui"));
        Assert.Equal(HttpStatusCode.OK, app3Res.StatusCode);

        var finalPo = await app3Res.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal(rev3Id, finalPo!.CurrentApprovedRevisionId);
        Assert.Equal(3, finalPo.CurrentApprovedRevisionNumber);
        Assert.Equal(73m, finalPo.ApprovedOrderedBaseQuantity); // 3*24 + 1

        var rev1After = finalPo.Revisions.Single(r => r.RevisionNumber == 1);
        Assert.Equal(PurchaseOrderRevisionStatuses.Superseded, rev1After.Status);
        Assert.Equal(rev1Hash, rev1After.SnapshotHash);
        Assert.Equal(rev1Grand, rev1After.GrandTotalAmount);
        Assert.Single(rev1After.Lines);

        var rev3Dto = finalPo.Revisions.Single(r => r.RevisionNumber == 3);
        Assert.NotNull(rev3Dto.DiffFromBaseline);
        Assert.Equal(1, rev3Dto.DiffFromBaseline!.BaselineRevisionNumber);
        Assert.Contains(rev3Dto.DiffFromBaseline.HeaderChanges, h => h.FieldKey == "deliveryDestination");
        Assert.Contains(rev3Dto.DiffFromBaseline.LineChanges, c => c.LineageLineKey == "L001" && c.ChangeKind == PoDiffChangeKinds.Modified);
        Assert.Contains(rev3Dto.DiffFromBaseline.LineChanges, c => c.LineageLineKey == "L002" && c.ChangeKind == PoDiffChangeKinds.Added);
    }

    [Fact]
    public async Task S05_API_04_MasterScopeOd01ToleranceDownstreamCancelBlockShortCloseAndRbac()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var readerA2 = await CreateSignedInClientAsync(factory, world.ReaderA2Email, world.Password);
        using var memberB1NoProc = await CreateSignedInClientAsync(factory, world.MemberB1NoProcEmail, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/procurement";
        var baseB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/procurement";

        // 1. Ineligible supplier & item checks (A1 supplier restricted out, sales-only item) + OD-01 block
        var badSupRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierRestrictedA1OnlyId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 10m, 20_000m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, badSupRes.StatusCode);
        Assert.Equal("supplier_not_eligible_for_entity", (await badSupRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        var badItemRes = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang", 30, null, null,
            [new PoLineInputDto(null, world.ItemSalesOnlyAId, world.PcsUomAId, 10m, 20_000m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, badItemRes.StatusCode);
        Assert.Equal("item_not_eligible_for_entity_purchase", (await badItemRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        var od01Res = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 10m, 20_000m, null, OverdeliveryTolerancePercent: 5m, AllowUnlimitedOverdelivery: true)]));
        Assert.Equal(HttpStatusCode.Conflict, od01Res.StatusCode);
        Assert.Equal("od01_overdelivery_tolerance_blocked", (await od01Res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // 2. Unreferenced PO can be cancelled; DELETE returns 409 po_delete_forbidden
        var draftCancelPo = await (await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang Cancel", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 5m, 20_000m, null)]))).Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, (await buyer.DeleteAsync($"{baseA1}/purchase-orders/{draftCancelPo!.PurchaseOrderId:D}")).StatusCode);

        var cancelOk = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{draftCancelPo.PurchaseOrderId:D}/cancel", new CloseOrCancelPoRequest("Dibatalkan sebelum kirim"));
        Assert.Equal(HttpStatusCode.OK, cancelOk.StatusCode);
        Assert.Equal(PurchaseOrderStatuses.Cancelled, (await cancelOk.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!.Status);

        // 3. Downstream-referenced approved PO blocks Cancel (409) and allows Short-Close (200 -> CLOSED)
        var refPo = await (await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang Short Close", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 20m, 20_000m, null)]))).Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{refPo!.PurchaseOrderId:D}/revisions/{refPo.Revisions[0].RevisionId:D}/submit", new SubmitPoRevisionRequest(null));
        await approver.PostAsJsonAsync($"{baseA1}/purchase-orders/{refPo.PurchaseOrderId:D}/revisions/{refPo.Revisions[0].RevisionId:D}/decide", new DecidePoRevisionRequest("APPROVED", "OK"));

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            var dbPo = await migDb.PurchaseOrders.SingleAsync(p => p.PurchaseOrderId == refPo.PurchaseOrderId);
            dbPo.RegisterDownstreamReference(10m, DateTimeOffset.UtcNow);
            await migDb.SaveChangesAsync();
        }

        var blockedCancel = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{refPo.PurchaseOrderId:D}/cancel", new CloseOrCancelPoRequest("Should fail"));
        Assert.Equal(HttpStatusCode.Conflict, blockedCancel.StatusCode);
        Assert.Equal("po_has_downstream_references", (await blockedCancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        var shortCloseOk = await buyer.PostAsJsonAsync($"{baseA1}/purchase-orders/{refPo.PurchaseOrderId:D}/short-close", new CloseOrCancelPoRequest("Sisa 10 PCS ditutup resmi"));
        Assert.Equal(HttpStatusCode.OK, shortCloseOk.StatusCode);
        var closedDto = await shortCloseOk.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        Assert.Equal(PurchaseOrderStatuses.Closed, closedDto!.Status);
        Assert.Equal(ReceivingProgressStatuses.ShortClosed, closedDto.ReceivingProgress);
        Assert.Equal(0m, closedDto.RemainingOpenBaseQuantity);

        // 4. RBAC & Cross-Tenant Isolation
        Assert.Equal(HttpStatusCode.OK, (await readerA2.GetAsync($"{baseA2}/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerA2.PostAsJsonAsync($"{baseA2}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAllAId, "IDR", new DateOnly(2026, 11, 18), null, "Read-only fail", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockedAId, world.PcsUomAId, 1m, 20_000m, null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberB1NoProc.GetAsync($"{baseB1}/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.GetAsync($"{baseB1}/purchase-orders")).StatusCode);
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tenantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
        cmd.Parameters.AddWithValue("tid", tenantId.ToString("D"));
        await cmd.ExecuteScalarAsync();
    }

    private static async Task<HttpClient> CreateSignedInClientAsync(WebApplicationFactory<Program> factory, string email, string password)
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

    private static async Task<S05SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (buyerA1Id, buyerA1Email, _) = await fx.CreateTestUserAsync("buyer.a1.s05@test.local", "Rina Procurement", pw);
        var (approverA1Id, approverA1Email, _) = await fx.CreateTestUserAsync("approver.a1.s05@test.local", "Budi Approver", pw);
        var (dualRoleA1Id, dualRoleA1Email, _) = await fx.CreateTestUserAsync("dual.a1.s05@test.local", "Sari DualRole", pw);
        var (readerA2Id, readerA2Email, _) = await fx.CreateTestUserAsync("reader.a2.s05@test.local", "Andi Reader", pw);
        var (memberB1NoProcId, memberB1NoProcEmail, _) = await fx.CreateTestUserAsync("noproc.b1.s05@test.local", "Dedi Member", pw);
        var (foreignMgrB1Id, foreignMgrB1Email, _) = await fx.CreateTestUserAsync("mgr.b1.s05@test.local", "Samudra Mgr", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, buyerA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approverA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, dualRoleA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readerA2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, memberB1NoProcId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignMgrB1Id, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.ProcurementManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.ProcurementApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualRoleA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualRoleA1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualRoleA1Id, PermissionCodes.ProcurementManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualRoleA1Id, PermissionCodes.ProcurementApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.ProcurementRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, memberB1NoProcId, PermissionCodes.WorkspaceView, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignMgrB1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignMgrB1Id, PermissionCodes.ProcurementManage, now, null, now));

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA2Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantBId, entityB1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now));

        var tolA1Id = Guid.NewGuid();
        var tolB1Id = Guid.NewGuid();
        db.DeliveryTolerancePolicies.AddRange(
            DeliveryTolerancePolicy.CreateSafeDefault(tolA1Id, tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now),
            DeliveryTolerancePolicy.CreateSafeDefault(tolB1Id, tenantBId, entityB1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));

        var supAllA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-ALL", "PT Sumber Makmur", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var supRestrA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-NO-A1", "PT Restricted Supplier", "Restricted", true, false, null, TaxPkpStatuses.NonPkp, EntityScopeModes.RestrictedEntities, now);
        var supB = Partner.Create(Guid.NewGuid(), tenantBId, "SMDR-SUP", "CV Samudra Supply", "Samudra Supply", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(supAllA, supRestrA, supB);

        var pcsA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var boxA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "BOX", "Box", 0, now);
        var pcsB = UnitOfMeasure.Create(Guid.NewGuid(), tenantBId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.AddRange(pcsA, boxA, pcsB);

        var itemStockA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-STK", "Karton Lipat", ItemKinds.StockedGood, pcsA.UomId, boxA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemSvcA = Item.Create(Guid.NewGuid(), tenantAId, "SRV-DEL", "Jasa Kirim", ItemKinds.NonStockService, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemSalesOnlyA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-SO", "Barang Khusus Jual", ItemKinds.StockedGood, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.RestrictedEntities, now);
        var itemB = Item.Create(Guid.NewGuid(), tenantBId, "SMDR-ITM", "Palet Samudra", ItemKinds.StockedGood, pcsB.UomId, pcsB.UomId, pcsB.UomId, EntityScopeModes.AllEntities, now);
        db.Items.AddRange(itemStockA, itemSvcA, itemSalesOnlyA, itemB);

        db.ItemUomConversions.Add(ItemUom.Create(Guid.NewGuid(), tenantAId, itemStockA.ItemId, boxA.UomId, 24m, now));
        db.ItemEntityPolicies.Add(ItemEntityPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, itemSalesOnlyA.ItemId, purchaseAllowed: false, salesAllowed: true, "Sales only", now));

        var taxId = Guid.NewGuid();
        db.TaxCodes.Add(TaxCode.Create(taxId, tenantAId, entityA1Id, "PPN-IN-11", "PPN Masukan 11%", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, null, new DateOnly(2025, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, "Test OD-07", now));

        await db.SaveChangesAsync();

        return new S05SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            buyerA1Id, buyerA1Email, approverA1Id, approverA1Email, dualRoleA1Id, dualRoleA1Email,
            readerA2Id, readerA2Email, memberB1NoProcId, memberB1NoProcEmail, foreignMgrB1Id, foreignMgrB1Email,
            supAllA.PartnerId, supRestrA.PartnerId, supB.PartnerId,
            pcsA.UomId, boxA.UomId, itemStockA.ItemId, itemSvcA.ItemId, itemSalesOnlyA.ItemId, itemB.ItemId, pcsB.UomId,
            taxId, tolA1Id, tolB1Id, pw);
    }
}
