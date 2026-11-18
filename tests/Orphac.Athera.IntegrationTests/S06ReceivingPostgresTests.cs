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
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.IntegrationTests;

public sealed class S06ReceivingPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S06SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid BuyerA1Id,
        string BuyerA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid ReceiverA1Id,
        string ReceiverA1Email,
        Guid ReaderA2Id,
        string ReaderA2Email,
        Guid MemberB1NoRcvId,
        string MemberB1NoRcvEmail,
        Guid ForeignReceiverB1Id,
        string ForeignReceiverB1Email,
        Guid SupplierAId,
        Guid SupplierBId,
        Guid PcsUomAId,
        Guid BoxUomAId,
        Guid ItemStockAId,
        Guid ForeignItemBId,
        Guid ForeignUomBId,
        Guid SeedPoA1Id,
        Guid SeedPoB1Id,
        Guid SeedGrnB1Id,
        string Password);

    [Fact]
    public async Task S06_PG_01_ForcedRlsMissingContextAndCrossTenantForeignKeysRejected()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. All 4 S06 tables enforce FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'procurement'
                  AND c.relname IN ('receipt_sequences', 'goods_receipts', 'goods_receipt_lines', 'dock_checks')
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(4L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id returns 0 rows and rejects INSERT with 42501
        await using (var zeroRead = conn.CreateCommand())
        {
            zeroRead.CommandText = "SELECT count(*) FROM procurement.goods_receipts;";
            Assert.Equal(0L, Convert.ToInt64(await zeroRead.ExecuteScalarAsync()));
        }

        await using (var insertNoCtx = conn.CreateCommand())
        {
            insertNoCtx.CommandText = """
                INSERT INTO procurement.receipt_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2026, 99);
                """;
            insertNoCtx.Parameters.AddWithValue("tid", world.TenantBId);
            insertNoCtx.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => insertNoCtx.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Scoped to TenantA, TenantB receipt rows are invisible and foreign write fails with 42501
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        await using (var readForeign = conn.CreateCommand())
        {
            readForeign.Transaction = tx;
            readForeign.CommandText = "SELECT count(*) FROM procurement.goods_receipts WHERE goods_receipt_id = @gid;";
            readForeign.Parameters.AddWithValue("gid", world.SeedGrnB1Id);
            Assert.Equal(0L, Convert.ToInt64(await readForeign.ExecuteScalarAsync()));
        }

        await using (var writeForeign = conn.CreateCommand())
        {
            writeForeign.Transaction = tx;
            writeForeign.CommandText = """
                INSERT INTO procurement.receipt_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2027, 1);
                """;
            writeForeign.Parameters.AddWithValue("tid", world.TenantBId);
            writeForeign.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeForeign.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await tx.RollbackAsync();

        // 4. Composite tenant/entity FK rejects TenantA receipt pointing to TenantB PO (SQLSTATE 23503)
        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using var fkCmd = migConn.CreateCommand();
        fkCmd.CommandText = """
            INSERT INTO procurement.goods_receipts (
                goods_receipt_id, tenant_id, legal_entity_id, receipt_number,
                purchase_order_id, po_revision_id, po_number_snapshot, po_revision_number_snapshot, po_snapshot_hash,
                supplier_partner_id, supplier_code_snapshot, supplier_legal_name_snapshot, supplier_delivery_note_number,
                idempotency_key, arrival_payload_hash, received_date, receiving_station, status,
                total_delivered_base_quantity, total_rejected_base_quantity, total_valued_accepted_base_quantity,
                total_unresolved_custody_base_quantity, total_released_base_quantity, total_held_base_quantity,
                has_financial_posting, received_by_user_id, created_at_utc, updated_at_utc, version)
            VALUES (
                @grid, @ta, @ea1, 'GRN-ROGUE-1',
                @pob, @pob, 'PO-B', 1, 'hash',
                @supa, 'SUP-A', 'Supplier A', 'SJ-ROGUE',
                'idem-fk', 'hash', DATE '2026-11-18', 'Dock A', 'ARRIVED',
                10, 0, 0, 10, 0, 0,
                false, @buyera1, now(), now(), 1);
            """;
        fkCmd.Parameters.AddWithValue("grid", Guid.NewGuid());
        fkCmd.Parameters.AddWithValue("ta", world.TenantAId);
        fkCmd.Parameters.AddWithValue("ea1", world.EntityA1Id);
        fkCmd.Parameters.AddWithValue("pob", world.SeedPoB1Id);
        fkCmd.Parameters.AddWithValue("supa", world.SupplierAId);
        fkCmd.Parameters.AddWithValue("buyera1", world.BuyerA1Id);

        var fkEx = await Assert.ThrowsAsync<PostgresException>(() => fkCmd.ExecuteNonQueryAsync());
        Assert.Equal("23503", fkEx.SqlState);
    }

    [Fact]
    public async Task S06_PG_02_MigrationM06DownAndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117205100_M05_PurchaseOrdersRevisionsAndFourEyes");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema = 'procurement'
                  AND table_name IN ('receipt_sequences', 'goods_receipts', 'goods_receipt_lines', 'dock_checks');
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
                WHERE table_schema = 'procurement'
                  AND table_name IN ('receipt_sequences', 'goods_receipts', 'goods_receipt_lines', 'dock_checks');
                """;
            Assert.Equal(4L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S06_PG_03_AppendOnlyDockChecksNoDeleteAndConservationCheckConstraintsEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var receiver = await CreateSignedInClientAsync(factory, world.ReceiverA1Email, world.Password);

        var po = await CreateAndApprovePoAsync(buyer, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 40m, "Dock Constraints");
        var poLine = po.Revisions[0].Lines[0];

        var grnRes = await receiver.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving/receipts",
            new RecordGoodsReceiptRequest(
                po.PurchaseOrderId,
                "SJ-IMMUT-01",
                "idem-immut-1",
                new DateOnly(2026, 11, 18),
                "Dermaga A",
                null,
                null,
                null,
                [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 20m, false, null, null, null)]));
        Assert.Equal(HttpStatusCode.Created, grnRes.StatusCode);
        var grn = await grnRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.NotNull(grn);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        // Runtime role lacks DELETE on goods_receipts, goods_receipt_lines, dock_checks AND UPDATE on dock_checks
        foreach (var sql in new[]
        {
            $"DELETE FROM procurement.dock_checks WHERE goods_receipt_id = '{grn.GoodsReceiptId:D}'",
            $"UPDATE procurement.dock_checks SET inspection_notes = 'tampered' WHERE goods_receipt_id = '{grn.GoodsReceiptId:D}'",
            $"DELETE FROM procurement.goods_receipt_lines WHERE goods_receipt_id = '{grn.GoodsReceiptId:D}'",
            $"DELETE FROM procurement.goods_receipts WHERE goods_receipt_id = '{grn.GoodsReceiptId:D}'"
        })
        {
            await using var spCmd = conn.CreateCommand();
            spCmd.Transaction = tx;
            spCmd.CommandText = "SAVEPOINT sp_test;";
            await spCmd.ExecuteNonQueryAsync();

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            var pgEx = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", pgEx.SqlState);

            await using var rbCmd = conn.CreateCommand();
            rbCmd.Transaction = tx;
            rbCmd.CommandText = "ROLLBACK TO SAVEPOINT sp_test;";
            await rbCmd.ExecuteNonQueryAsync();
        }

        // Direct SQL violating conservation CHECK constraint rejected with 23514
        await using (var sp2 = conn.CreateCommand())
        {
            sp2.Transaction = tx;
            sp2.CommandText = "SAVEPOINT sp_cons;";
            await sp2.ExecuteNonQueryAsync();
        }
        await using (var consCmd = conn.CreateCommand())
        {
            consCmd.Transaction = tx;
            consCmd.CommandText = "UPDATE procurement.goods_receipt_lines SET dock_released_base_quantity = 999 WHERE goods_receipt_line_id = @lid;";
            consCmd.Parameters.AddWithValue("lid", grn.Lines[0].GoodsReceiptLineId);
            var consEx = await Assert.ThrowsAsync<PostgresException>(() => consCmd.ExecuteNonQueryAsync());
            Assert.Equal("23514", consEx.SqlState);
        }
        await using (var rb2 = conn.CreateCommand())
        {
            rb2.Transaction = tx;
            rb2.CommandText = "ROLLBACK TO SAVEPOINT sp_cons;";
            await rb2.ExecuteNonQueryAsync();
        }

        // Direct SQL attempting S06 valued_accepted > 0 rejected by ck_gr_lines_s06_unvalued (23514)
        await using var valCmd = conn.CreateCommand();
        valCmd.Transaction = tx;
        valCmd.CommandText = "UPDATE procurement.goods_receipt_lines SET valued_accepted_base_quantity = 5, dock_released_base_quantity = 5, pending_check_base_quantity = 15, unresolved_custody_base_quantity = 15 WHERE goods_receipt_line_id = @lid;";
        valCmd.Parameters.AddWithValue("lid", grn.Lines[0].GoodsReceiptLineId);
        var valEx = await Assert.ThrowsAsync<PostgresException>(() => valCmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", valEx.SqlState);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task S06_API_01_PartialDeliveriesDamageRejectSplitPoCancelBlockedAndZeroFinanceEffects()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var receiver = await CreateSignedInClientAsync(factory, world.ReceiverA1Email, world.Password);

        var po = await CreateAndApprovePoAsync(buyer, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 100m, "Dock Partial Test");
        var poLine = po.Revisions[0].Lines[0];
        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";

        // 1. Partial arrival #1: 60 PCS of 100 PCS
        var arr1Res = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            po.PurchaseOrderId,
            "SJ-PARTIAL-01",
            "idem-partial-1",
            new DateOnly(2026, 11, 18),
            "Dermaga Timur 1",
            "B 8899 NUSA",
            "SCAN-SJ-01.pdf",
            "Parsial pertama",
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 60m, true, null, "Perlu inspeksi palet", null)]));

        Assert.Equal(HttpStatusCode.Created, arr1Res.StatusCode);
        var grn1 = await arr1Res.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.NotNull(grn1);
        Assert.Equal(GoodsReceiptStatuses.InDockInspection, grn1.Status);
        Assert.False(grn1.HasFinancialPosting);
        Assert.Equal(0, grn1.LinkedGlJournalCount);

        // 2. Dock Check: 55 PCS DOCK_RELEASED, 5 PCS DOCK_REJECTED (Damaged)
        var chkRes = await receiver.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn1.GoodsReceiptId:D}/lines/{grn1.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                "chk-partial-split-1",
                0m,
                0m,
                55m,
                5m,
                DiscrepancyReasonCodes.DamagedPackaging,
                "5 PCS penyok sudut, 55 PCS layak diteruskan",
                "FOTO-DAMAGE-01.jpg"));
        Assert.Equal(HttpStatusCode.OK, chkRes.StatusCode);
        var checkedGrn1 = await chkRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.NotNull(checkedGrn1);

        var checkedLine1 = Assert.Single(checkedGrn1.Lines);
        Assert.Equal(60m, checkedLine1.PhysicallyDeliveredBaseQuantity);
        Assert.Equal(55m, checkedLine1.DockReleasedBaseQuantity);
        Assert.Equal(5m, checkedLine1.DockRejectedBaseQuantity);
        Assert.Equal(0m, checkedLine1.ValuedAcceptedBaseQuantity);
        Assert.Equal(55m, checkedLine1.UnresolvedCustodyBaseQuantity);
        Assert.Equal(DockCustodyStatuses.DockReleased, checkedLine1.CustodyStatus);
        Assert.Equal(GoodsReceiptStatuses.ReleasedForAcceptance, checkedGrn1.Status);
        Assert.Equal(0, checkedGrn1.LinkedGlJournalCount);
        Assert.Equal(2, checkedGrn1.DockChecks.Count);

        // 3. Parent PO cancellation now blocked due to Gate 1 downstream reference
        var cancelRes = await buyer.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement/purchase-orders/{po.PurchaseOrderId:D}/cancel",
            new CloseOrCancelPoRequest("Attempt cancel after dock arrival"));
        Assert.Equal(HttpStatusCode.Conflict, cancelRes.StatusCode);
        Assert.Equal("po_has_downstream_references", (await cancelRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // 4. Eligible PO query reflects 55 PCS released / 45 PCS remaining (5 rejected stays open)
        var eligible = await (await receiver.GetAsync($"{rcvBase}/eligible-pos")).Content.ReadFromJsonAsync<EligibleReceivingPosResponse>(JsonOptions);
        var eligiblePo = eligible!.Items.Single(p => p.PurchaseOrderId == po.PurchaseOrderId);
        Assert.Equal(45m, eligiblePo.Lines[0].RemainingEligibleBaseQuantity);

        // 5. Partial arrival #2 (replacement/balance): 45 PCS arrives without excess hold
        var arr2Res = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            po.PurchaseOrderId,
            "SJ-PARTIAL-02",
            "idem-partial-2",
            new DateOnly(2026, 11, 18),
            "Dermaga Timur 1",
            null,
            null,
            null,
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 45m, false, null, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arr2Res.StatusCode);
        var grn2 = await arr2Res.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.Equal(0m, grn2!.Lines[0].OverdeliveryExcessBaseQuantity);
        Assert.Equal(DockCustodyStatuses.Arrived, grn2.Lines[0].CustodyStatus);

        // 6. Verify ZERO GL journals or GRNI entries created in real PostgreSQL
        await using var migDb = fx.CreateMigratorDbContext();
        Assert.Equal(0, await migDb.JournalEntries.CountAsync(j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id));
        Assert.Equal(0, await migDb.JournalLines.CountAsync(jl => jl.TenantId == world.TenantAId && jl.LegalEntityId == world.EntityA1Id));
    }

    [Fact]
    public async Task S06_API_02_OverdeliveryHeldUnderOd01BlocksOverReleaseAndUnblocksOnlyAfterApprovedAmendment()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var receiver = await CreateSignedInClientAsync(factory, world.ReceiverA1Email, world.Password);

        // Approved PO: 48 PCS (2 BOX)
        var po = await CreateAndApprovePoAsync(buyer, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.BoxUomAId, 2m, "OD-01 Test");
        var poLine = po.Revisions[0].Lines[0];
        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var procBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";

        // Deliver 60 PCS (48 eligible + 12 OD-01 excess)
        var arrRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            po.PurchaseOrderId,
            "SJ-EXCESS-01",
            "idem-od01-1",
            new DateOnly(2026, 11, 18),
            "Dermaga Barat",
            null,
            null,
            null,
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 60m, false, null, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        var line = Assert.Single(grn!.Lines);

        Assert.Equal(48m, line.PendingCheckBaseQuantity);
        Assert.Equal(12m, line.DockHeldBaseQuantity);
        Assert.Equal(12m, line.OverdeliveryExcessBaseQuantity);
        Assert.Equal(DiscrepancyReasonCodes.OverdeliveryOd01Hold, line.DiscrepancyReasonCode);
        Assert.Equal(DockCustodyStatuses.DockCheckPending, line.CustodyStatus);

        // Releasing 49 or 60 PCS fails with OD-01 block
        foreach (var badRelease in new[] { 49m, 60m })
        {
            var blockedRes = await receiver.PostAsJsonAsync(
                $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{line.GoodsReceiptLineId:D}/dock-checks",
                new RecordDockCheckRequest(
                    $"chk-od01-fail-{badRelease}",
                    0m,
                    60m - badRelease,
                    badRelease,
                    0m,
                    DiscrepancyReasonCodes.OverdeliveryOd01Hold,
                    "Attempt release excess",
                    null));
            Assert.Equal(HttpStatusCode.Conflict, blockedRes.StatusCode);
            Assert.Equal("od01_overdelivery_release_blocked", (await blockedRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
        }

        // Releasing 48 PCS while keeping 12 PCS in DOCK_HELD succeeds
        var partialOkRes = await receiver.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{line.GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                "chk-od01-hold12",
                0m,
                12m,
                48m,
                0m,
                DiscrepancyReasonCodes.OverdeliveryOd01Hold,
                "Lepas 48 PCS sesuai PO, tahan 12 PCS berlebih",
                null));
        Assert.Equal(HttpStatusCode.OK, partialOkRes.StatusCode);
        var heldGrn = await partialOkRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.Equal(DockCustodyStatuses.DockHeld, heldGrn!.Lines[0].CustodyStatus);
        Assert.Equal(48m, heldGrn.Lines[0].DockReleasedBaseQuantity);
        Assert.Equal(12m, heldGrn.Lines[0].DockHeldBaseQuantity);

        // Amend PO Rev 2 to 60 PCS and approve independently
        var amendRes = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions", new CreatePoAmendmentRequest("Add 12 PCS for supplier overdelivery"));
        Assert.Equal(HttpStatusCode.Created, amendRes.StatusCode);
        var amendedPo = await amendRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        var rev2 = amendedPo!.Revisions.Single(r => r.RevisionNumber == 2);

        var updRes = await buyer.PutAsJsonAsync(
            $"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2.RevisionId:D}",
            new UpdatePurchaseOrderRevisionRequest(
                world.SupplierAId,
                new DateOnly(2026, 11, 18),
                new DateOnly(2026, 11, 25),
                "OD-01 Test",
                30,
                null,
                "Raise commitment to 60 PCS",
                null,
                [new PoLineInputDto("L001", world.ItemStockAId, world.PcsUomAId, 60m, 20_000m, null)]));
        Assert.Equal(HttpStatusCode.OK, updRes.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync($"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2.RevisionId:D}/submit", new SubmitPoRevisionRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync($"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{rev2.RevisionId:D}/decide", new DecidePoRevisionRequest("APPROVED", "Approve +12 PCS"))).StatusCode);

        // Now releasing full 60 PCS succeeds cleanly and clears excess flag
        var fullReleaseRes = await receiver.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{line.GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                "chk-od01-release60",
                0m,
                0m,
                60m,
                0m,
                DiscrepancyReasonCodes.None,
                "Semua 60 PCS dirilis setelah Rev 2 disetujui",
                null));
        Assert.Equal(HttpStatusCode.OK, fullReleaseRes.StatusCode);
        var releasedGrn = await fullReleaseRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.Equal(DockCustodyStatuses.DockReleased, releasedGrn!.Lines[0].CustodyStatus);
        Assert.Equal(60m, releasedGrn.Lines[0].DockReleasedBaseQuantity);
        Assert.Equal(0m, releasedGrn.Lines[0].OverdeliveryExcessBaseQuantity);
        Assert.Equal(GoodsReceiptStatuses.ReleasedForAcceptance, releasedGrn.Status);
    }

    [Fact]
    public async Task S06_API_03_IdempotencyDuplicateDeliveryNoteUnapprovedPoAndConcurrentReleaseRaceSafe()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var receiver = await CreateSignedInClientAsync(factory, world.ReceiverA1Email, world.Password);

        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var procBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/procurement";

        // 1. Unapproved DRAFT PO rejected for arrival
        var draftPo = await (await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierAId, "IDR", new DateOnly(2026, 11, 18), null, "Gudang", 30, null, null,
            [new PoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 10m, 20_000m, null)]))).Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);
        var draftArrivalRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            draftPo!.PurchaseOrderId, "SJ-DRAFT", "idem-draft", new DateOnly(2026, 11, 18), "Dock", null, null, null,
            [new ArrivalLineRequest(draftPo.Revisions[0].Lines[0].PoLineId, world.PcsUomAId, 5m, false, null, null, null)]));
        Assert.Equal(HttpStatusCode.Conflict, draftArrivalRes.StatusCode);
        Assert.Equal("po_not_eligible_for_receiving", (await draftArrivalRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // 2. Approved 48 PCS PO for idempotency & concurrency checks
        var po = await CreateAndApprovePoAsync(buyer, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 48m, "Concurrency Dock");
        var poLine = po.Revisions[0].Lines[0];

        var req1 = new RecordGoodsReceiptRequest(
            po.PurchaseOrderId,
            "SJ-IDEM-01",
            "idem-key-alpha",
            new DateOnly(2026, 11, 18),
            "Dock A",
            null,
            null,
            null,
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 30m, false, null, null, null)]);

        var firstRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", req1);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);
        var firstGrn = await firstRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);

        // Exact retry returns 200 OK replay without extra reference or outbox
        var replayRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", req1);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayGrn = await replayRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.True(replayGrn!.WasIdempotentReplay);
        Assert.Equal(firstGrn!.GoodsReceiptId, replayGrn.GoodsReceiptId);

        // Same key with mutated payload returns 409 conflict
        var conflictRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", req1 with { ReceivingStation = "Dock B" });
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);
        Assert.Equal("duplicate_receipt_idempotency_conflict", (await conflictRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // New key with same supplier Surat Jalan returns 409 duplicate_supplier_delivery_note
        var dupSjRes = await receiver.PostAsJsonAsync($"{rcvBase}/receipts", req1 with { IdempotencyKey = "idem-key-beta" });
        Assert.Equal(HttpStatusCode.Conflict, dupSjRes.StatusCode);
        Assert.Equal("duplicate_supplier_delivery_note", (await dupSjRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // Second receipt arrives cleanly with 30 PCS (18 eligible pending + 12 excess held)
        var secondGrn = await (await receiver.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            po.PurchaseOrderId,
            "SJ-RACE-02",
            "idem-race-2",
            new DateOnly(2026, 11, 18),
            "Dock B",
            null,
            null,
            null,
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomAId, 30m, false, null, null, null)]))).Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);

        // Release full 30 on receipt 1 first
        var r1Chk = new RecordDockCheckRequest("chk-race-r1", 0m, 0m, 30m, 0m, DiscrepancyReasonCodes.None, "Release 30 on grn1", null);
        Assert.Equal(HttpStatusCode.OK, (await receiver.PostAsJsonAsync($"{rcvBase}/receipts/{firstGrn.GoodsReceiptId:D}/lines/{firstGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks", r1Chk)).StatusCode);

        // Concurrent 6x race trying to release 25 PCS (which would overshoot 48 total!) vs 18 PCS across unique keys on receipt 2
        var raceTasks = Enumerable.Range(0, 6).Select(i => receiver.PostAsJsonAsync(
            $"{rcvBase}/receipts/{secondGrn!.GoodsReceiptId:D}/lines/{secondGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                $"chk-concurrent-{i}",
                0m,
                5m,
                25m,
                0m,
                DiscrepancyReasonCodes.OverdeliveryOd01Hold,
                "Concurrent overshoot probe",
                null)));
        var raceResponses = await Task.WhenAll(raceTasks);
        Assert.All(raceResponses, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        // Now dock check exact remaining 18 PCS + 12 held -> replay verifies idempotent 200
        var chkExact = new RecordDockCheckRequest("chk-r2-exact18", 0m, 12m, 18m, 0m, DiscrepancyReasonCodes.OverdeliveryOd01Hold, "Release 18 only", null);
        Assert.Equal(HttpStatusCode.OK, (await receiver.PostAsJsonAsync($"{rcvBase}/receipts/{secondGrn!.GoodsReceiptId:D}/lines/{secondGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks", chkExact)).StatusCode);
        var chkReplay = await receiver.PostAsJsonAsync($"{rcvBase}/receipts/{secondGrn.GoodsReceiptId:D}/lines/{secondGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks", chkExact);
        Assert.Equal(HttpStatusCode.OK, chkReplay.StatusCode);
        Assert.True((await chkReplay.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!.WasIdempotentReplay);
    }

    [Fact]
    public async Task S06_API_04_RbacAndCrossTenantBoundariesEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var receiverA1 = await CreateSignedInClientAsync(factory, world.ReceiverA1Email, world.Password);
        using var readerA2 = await CreateSignedInClientAsync(factory, world.ReaderA2Email, world.Password);
        using var memberB1NoRcv = await CreateSignedInClientAsync(factory, world.MemberB1NoRcvEmail, world.Password);

        var rcvA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/receiving";
        var rcvB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/receiving";

        // Reader with receiving.read can GET but gets 403 on POST
        Assert.Equal(HttpStatusCode.OK, (await readerA2.GetAsync($"{rcvA2}/receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerA2.PostAsJsonAsync($"{rcvA2}/receipts", new RecordGoodsReceiptRequest(
            world.SeedPoA1Id, "SJ-DENY", "idem-deny", new DateOnly(2026, 11, 18), "Dock", null, null, null, []))).StatusCode);

        // Tenant member without receiving grants gets 403
        Assert.Equal(HttpStatusCode.Forbidden, (await memberB1NoRcv.GetAsync($"{rcvB1}/receipts")).StatusCode);

        // Cross-tenant access gets 404 with zero leakage
        var crossRes = await receiverA1.GetAsync($"{rcvB1}/receipts");
        Assert.Equal(HttpStatusCode.NotFound, crossRes.StatusCode);
        var body = await crossRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SMDR", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<PurchaseOrderDetailDto> CreateAndApprovePoAsync(
        HttpClient buyer,
        HttpClient approver,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal qty,
        string destination)
    {
        var procBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/procurement";
        var createRes = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            supplierId, "IDR", new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 25), destination, 30, null, null,
            [new PoLineInputDto("L001", itemId, uomId, qty, 20_000m, null)]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draft = await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);

        var submitRes = await buyer.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft!.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null));
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        var decideRes = await approver.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Approved for receiving tests"));
        Assert.Equal(HttpStatusCode.OK, decideRes.StatusCode);
        return (await decideRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;
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

    private static async Task<S06SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (buyerA1Id, buyerA1Email, _) = await fx.CreateTestUserAsync("buyer.a1.s06@test.local", "Rina Buyer", pw);
        var (approverA1Id, approverA1Email, _) = await fx.CreateTestUserAsync("approver.a1.s06@test.local", "Budi Approver", pw);
        var (receiverA1Id, receiverA1Email, _) = await fx.CreateTestUserAsync("receiver.a1.s06@test.local", "Agus Dock", pw);
        var (readerA2Id, readerA2Email, _) = await fx.CreateTestUserAsync("reader.a2.s06@test.local", "Andi Reader", pw);
        var (memberB1NoRcvId, memberB1NoRcvEmail, _) = await fx.CreateTestUserAsync("norcv.b1.s06@test.local", "Dedi Member", pw);
        var (foreignRcvB1Id, foreignRcvB1Email, _) = await fx.CreateTestUserAsync("rcv.b1.s06@test.local", "Samudra Dock", pw);

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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, receiverA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readerA2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, memberB1NoRcvId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignRcvB1Id, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerA1Id, PermissionCodes.ProcurementManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, PermissionCodes.ProcurementApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, receiverA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, receiverA1Id, PermissionCodes.ReceivingRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, receiverA1Id, PermissionCodes.ReceivingManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.ReceivingRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, memberB1NoRcvId, PermissionCodes.WorkspaceView, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignRcvB1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignRcvB1Id, PermissionCodes.ProcurementRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignRcvB1Id, PermissionCodes.ProcurementManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignRcvB1Id, PermissionCodes.ReceivingRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignRcvB1Id, PermissionCodes.ReceivingManage, now, null, now));

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA2Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantBId, entityB1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now));

        db.DeliveryTolerancePolicies.AddRange(
            DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now),
            DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantBId, entityB1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));

        var supA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-A", "PT Sumber Makmur", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var supB = Partner.Create(Guid.NewGuid(), tenantBId, "SUP-B", "CV Samudra Supply", "Samudra Supply", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(supA, supB);

        var pcsA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var boxA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "BOX", "Box", 0, now);
        var pcsB = UnitOfMeasure.Create(Guid.NewGuid(), tenantBId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.AddRange(pcsA, boxA, pcsB);

        var itemA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-STK", "Karton Lipat", ItemKinds.StockedGood, pcsA.UomId, boxA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemB = Item.Create(Guid.NewGuid(), tenantBId, "SMDR-ITM", "Palet Samudra", ItemKinds.StockedGood, pcsB.UomId, pcsB.UomId, pcsB.UomId, EntityScopeModes.AllEntities, now);
        db.Items.AddRange(itemA, itemB);
        db.ItemUomConversions.Add(ItemUom.Create(Guid.NewGuid(), tenantAId, itemA.ItemId, boxA.UomId, 24m, now));
        await db.SaveChangesAsync();

        // Seed approved POs in A1 & B1 + a B1 receipt for cross-tenant RLS isolation checks
        var seedPoA1 = await PurchaseOrderWorkflowService.CreateDraftAsync(
            db,
            new CreatePoDraftCommand(tenantAId, entityA1Id, "IDR", "IDR", supA.PartnerId, new DateOnly(2026, 11, 18), null, "Gudang A1", 30, null, null,
                [new PoLineInput("L001", itemA.ItemId, pcsA.UomId, 10m, 10_000m, null)], buyerA1Id, now),
            CancellationToken.None);
        await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantAId, entityA1Id, seedPoA1.PurchaseOrder!.PurchaseOrderId, seedPoA1.Revision!.RevisionId, null, buyerA1Id, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantAId, entityA1Id, seedPoA1.PurchaseOrder.PurchaseOrderId, seedPoA1.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded", approverA1Id, now), CancellationToken.None);

        var seedPoB1 = await PurchaseOrderWorkflowService.CreateDraftAsync(
            db,
            new CreatePoDraftCommand(tenantBId, entityB1Id, "IDR", "IDR", supB.PartnerId, new DateOnly(2026, 11, 18), null, "Gudang B1", 30, null, null,
                [new PoLineInput("L001", itemB.ItemId, pcsB.UomId, 20m, 10_000m, null)], foreignRcvB1Id, now),
            CancellationToken.None);
        await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantBId, entityB1Id, seedPoB1.PurchaseOrder!.PurchaseOrderId, seedPoB1.Revision!.RevisionId, null, foreignRcvB1Id, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantBId, entityB1Id, seedPoB1.PurchaseOrder.PurchaseOrderId, seedPoB1.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded B1", memberB1NoRcvId, now), CancellationToken.None);

        var bLine = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == seedPoB1.Revision.RevisionId);
        var bGrn = await GoodsReceiptWorkflowService.RecordArrivalAsync(
            db,
            new RecordGoodsReceiptCommand(tenantBId, entityB1Id, seedPoB1.PurchaseOrder.PurchaseOrderId, "SJ-B1-SEED", "idem-b1-seed", new DateOnly(2026, 11, 18), "Dock B1", null, null, null,
                [new ArrivalLineInput(bLine.PoLineId, pcsB.UomId, 10m, false, null, null, null)], foreignRcvB1Id, now),
            CancellationToken.None);

        return new S06SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            buyerA1Id, buyerA1Email, approverA1Id, approverA1Email, receiverA1Id, receiverA1Email,
            readerA2Id, readerA2Email, memberB1NoRcvId, memberB1NoRcvEmail, foreignRcvB1Id, foreignRcvB1Email,
            supA.PartnerId, supB.PartnerId, pcsA.UomId, boxA.UomId, itemA.ItemId, itemB.ItemId, pcsB.UomId,
            seedPoA1.PurchaseOrder.PurchaseOrderId, seedPoB1.PurchaseOrder.PurchaseOrderId, bGrn.Receipt!.GoodsReceiptId, pw);
    }
}
