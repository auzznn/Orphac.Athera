using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.IntegrationTests;

public sealed class S07ValuedAcceptancePostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S07SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid OperatorA1Id,
        string OperatorA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid DockOnlyA1Id,
        string DockOnlyA1Email,
        Guid ReaderA2Id,
        string ReaderA2Email,
        Guid ForeignOperatorB1Id,
        string ForeignOperatorB1Email,
        Guid SupplierAId,
        Guid SupplierBId,
        Guid PcsUomAId,
        Guid ItemStockAId,
        Guid ForeignItemBId,
        Guid SeedAccA1Id,
        Guid SeedPoA1Id,
        Guid SeedGrnB1Id,
        string Password);

    [Fact]
    public async Task S07_PG_01_SchemaRlsAppendOnlyAndDeferredBackingTriggerEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. All 11 S07 tables enforce ENABLE + FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'procurement' AND c.relname IN ('acceptance_sequences', 'valued_acceptances', 'valued_acceptance_lines'))
                    OR (n.nspname = 'inventory' AND c.relname IN ('movement_sequences', 'stock_positions', 'valuation_pools', 'stock_movements', 'movement_lines', 'valuation_entries'))
                    OR (n.nspname = 'gl' AND c.relname IN ('receipt_accruals', 'receipt_accrual_lines'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id denies reads (0 rows) and writes (42501)
        await using (var zeroRead = conn.CreateCommand())
        {
            zeroRead.CommandText = "SELECT count(*) FROM procurement.valued_acceptances;";
            Assert.Equal(0L, Convert.ToInt64(await zeroRead.ExecuteScalarAsync()));
        }

        await using (var noCtxWrite = conn.CreateCommand())
        {
            noCtxWrite.CommandText = """
                INSERT INTO inventory.movement_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2026, 1);
                """;
            noCtxWrite.Parameters.AddWithValue("tid", world.TenantBId);
            noCtxWrite.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => noCtxWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Scoped to Tenant B: Tenant A valued acceptance is invisible; scoped to Tenant A: UPDATE/DELETE on append-only tables denied with 42501
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, tx, world.TenantBId);
            await using var readForeign = conn.CreateCommand();
            readForeign.Transaction = tx;
            readForeign.CommandText = "SELECT count(*) FROM procurement.valued_acceptances WHERE valued_acceptance_id = @aid;";
            readForeign.Parameters.AddWithValue("aid", world.SeedAccA1Id);
            Assert.Equal(0L, Convert.ToInt64(await readForeign.ExecuteScalarAsync()));
            await tx.RollbackAsync();
        }

        await using (var txA = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, txA, world.TenantAId);

            foreach (var sql in new[]
            {
                "UPDATE procurement.valued_acceptances SET stock_status = 'BLOCKED' WHERE valued_acceptance_id = @aid;",
                "DELETE FROM procurement.valued_acceptances WHERE valued_acceptance_id = @aid;",
                "DELETE FROM procurement.valued_acceptance_lines WHERE valued_acceptance_id = @aid;",
                "UPDATE inventory.stock_movements SET notes = 'tampered' WHERE source_document_id = @aid;",
                "DELETE FROM inventory.movement_lines WHERE tenant_id = @tid;",
                "UPDATE inventory.valuation_entries SET new_moving_average_cost = 1 WHERE source_document_id = @aid;",
                "DELETE FROM inventory.valuation_entries WHERE source_document_id = @aid;"
            })
            {
                await using var sp = conn.CreateCommand();
                sp.Transaction = txA;
                sp.CommandText = "SAVEPOINT sp_append_check;";
                await sp.ExecuteNonQueryAsync();

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = txA;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("aid", world.SeedAccA1Id);
                cmd.Parameters.AddWithValue("tid", world.TenantAId);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Equal("42501", ex.SqlState);

                await using var rb = conn.CreateCommand();
                rb.Transaction = txA;
                rb.CommandText = "ROLLBACK TO SAVEPOINT sp_append_check;";
                await rb.ExecuteNonQueryAsync();
            }

            // OD-04 DB CHECK constraint rejects orphan carrying value with zero quantity (SQLSTATE 23514)
            await using (var sp2 = conn.CreateCommand())
            {
                sp2.Transaction = txA;
                sp2.CommandText = "SAVEPOINT sp_od04_ck;";
                await sp2.ExecuteNonQueryAsync();
            }
            await using (var od04Cmd = conn.CreateCommand())
            {
                od04Cmd.Transaction = txA;
                od04Cmd.CommandText = """
                    INSERT INTO inventory.valuation_pools (
                        valuation_pool_id, tenant_id, legal_entity_id, item_id, currency_code, valuation_group_code,
                        base_uom_id, status, valued_base_quantity, carrying_value_amount, moving_average_unit_cost,
                        last_entry_sequence, updated_at_utc, version)
                    VALUES (
                        @vpid, @tid, @eid, @iid, 'IDR', 'OD04_PROBE', @uid, 'ACTIVE', 0, 50000, 0, 1, now(), 1);
                    """;
                od04Cmd.Parameters.AddWithValue("vpid", Guid.NewGuid());
                od04Cmd.Parameters.AddWithValue("tid", world.TenantAId);
                od04Cmd.Parameters.AddWithValue("eid", world.EntityA2Id);
                od04Cmd.Parameters.AddWithValue("iid", world.ItemStockAId);
                od04Cmd.Parameters.AddWithValue("uid", world.PcsUomAId);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => od04Cmd.ExecuteNonQueryAsync());
                Assert.Equal("23514", ex.SqlState);
            }
            await using (var rb2 = conn.CreateCommand())
            {
                rb2.Transaction = txA;
                rb2.CommandText = "ROLLBACK TO SAVEPOINT sp_od04_ck;";
                await rb2.ExecuteNonQueryAsync();
            }

            await txA.RollbackAsync();
        }

        // 4. Composite FK rejects cross-tenant item link and deferred trigger trg_valued_acceptance_backing blocks orphan commit (23514)
        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using (var fkCmd = migConn.CreateCommand())
        {
            fkCmd.CommandText = """
                INSERT INTO inventory.stock_positions (
                    stock_position_id, tenant_id, legal_entity_id, item_id, valuation_group_code, base_uom_id,
                    unrestricted_base_quantity, quality_inspection_base_quantity, blocked_base_quantity,
                    reserved_base_quantity, total_valued_on_hand_base_quantity, available_base_quantity, updated_at_utc, version)
                VALUES (@spid, @ta, @ea1, @foreignItemB, 'DEFAULT', @pcsa, 0, 0, 0, 0, 0, 0, now(), 1);
                """;
            fkCmd.Parameters.AddWithValue("spid", Guid.NewGuid());
            fkCmd.Parameters.AddWithValue("ta", world.TenantAId);
            fkCmd.Parameters.AddWithValue("ea1", world.EntityA1Id);
            fkCmd.Parameters.AddWithValue("foreignItemB", world.ForeignItemBId);
            fkCmd.Parameters.AddWithValue("pcsa", world.PcsUomAId);
            var fkEx = await Assert.ThrowsAsync<PostgresException>(() => fkCmd.ExecuteNonQueryAsync());
            Assert.Equal("23503", fkEx.SqlState);
        }

        await using (var txTrg = await migConn.BeginTransactionAsync())
        {
            var fakeAccId = Guid.NewGuid();
            await using var insOrphan = migConn.CreateCommand();
            insOrphan.Transaction = txTrg;
            insOrphan.CommandText = """
                INSERT INTO procurement.valued_acceptances (
                    valued_acceptance_id, tenant_id, legal_entity_id, acceptance_number,
                    goods_receipt_id, purchase_order_id, po_revision_id, supplier_partner_id,
                    acceptance_date, stock_status, currency_code, idempotency_key, payload_hash,
                    total_accepted_base_quantity, total_accepted_amount,
                    stock_movement_id, receipt_accrual_id, gl_journal_id, accepted_by_user_id, created_at_utc)
                SELECT
                    @vaid, tenant_id, legal_entity_id, 'ACC-ORPHAN-999',
                    goods_receipt_id, purchase_order_id, po_revision_id, supplier_partner_id,
                    DATE '2026-11-18', 'UNRESTRICTED', 'IDR', 'orphan-key', 'orphan-hash',
                    1, 1000, @fakeMov, @fakeAcc, @fakeJrn, @uid, now()
                FROM procurement.valued_acceptances
                WHERE valued_acceptance_id = @seedAcc;
                """;
            insOrphan.Parameters.AddWithValue("vaid", fakeAccId);
            insOrphan.Parameters.AddWithValue("fakeMov", Guid.NewGuid());
            insOrphan.Parameters.AddWithValue("fakeAcc", Guid.NewGuid());
            insOrphan.Parameters.AddWithValue("fakeJrn", Guid.NewGuid());
            insOrphan.Parameters.AddWithValue("uid", world.OperatorA1Id);
            insOrphan.Parameters.AddWithValue("seedAcc", world.SeedAccA1Id);
            await insOrphan.ExecuteNonQueryAsync();

            var trgEx = await Assert.ThrowsAsync<PostgresException>(() => txTrg.CommitAsync());
            Assert.Equal("23514", trgEx.SqlState);
        }
    }

    [Fact]
    public async Task S07_PG_02_MigrationM07DownAndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117214046_M06_DockCustodyAndReceivingGateOne");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = """
                SELECT count(*)
                FROM information_schema.tables
                WHERE table_schema = 'inventory'
                   OR (table_schema = 'procurement' AND table_name IN ('acceptance_sequences', 'valued_acceptances', 'valued_acceptance_lines'))
                   OR (table_schema = 'gl' AND table_name IN ('receipt_accruals', 'receipt_accrual_lines'));
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCheck = conn.CreateCommand();
            upCheck.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'procurement' AND c.relname IN ('acceptance_sequences', 'valued_acceptances', 'valued_acceptance_lines'))
                    OR (n.nspname = 'inventory' AND c.relname IN ('movement_sequences', 'stock_positions', 'valuation_pools', 'stock_movements', 'movement_lines', 'valuation_entries'))
                    OR (n.nspname = 'gl' AND c.relname IN ('receipt_accruals', 'receipt_accrual_lines'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S07_API_01_PreviewMultiBatchMovingAverageGrniGlAndFaultRollbackVerified()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var op = await CreateSignedInClientAsync(factory, world.OperatorA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);

        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var invBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";

        // Create and release Batch 2 PO (10 PCS @ IDR 130,000 = 1,300,000 on top of seeded Batch 1: 10 PCS @ 100,000 = 1,000,000)
        var (po2, line2) = await CreateApprovedPoAsync(op, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 10m, 130_000m);
        var grn2 = await RecordArrivalAndReleaseAsync(op, rcvBase, po2.PurchaseOrderId, line2.PoLineId, world.PcsUomAId, "SJ-BATCH-02", "idem-gr2", 10m, 10m);
        var grLine2Id = grn2.Lines[0].GoodsReceiptLineId;

        // 1. Preview against CLOSED period 2026-10 returns CanPost = false
        var closedPreviewRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}/accept-preview",
            new PreviewReceiptAcceptanceRequest(new DateOnly(2026, 10, 15), StockStatuses.Unrestricted, [new AcceptReceiptLineRequest(grLine2Id, 6m)]));
        Assert.Equal(HttpStatusCode.OK, closedPreviewRes.StatusCode);
        var closedPreview = await closedPreviewRes.Content.ReadFromJsonAsync<AcceptancePreviewDto>(JsonOptions);
        Assert.False(closedPreview!.CanPost);
        Assert.Contains(closedPreview.BlockingReasons, b => b.Contains("period_closed", StringComparison.Ordinal));

        // 2. Posting against CLOSED period returns 409 and commits ZERO partial rows
        var closedPostRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-closed-1", new DateOnly(2026, 10, 15), StockStatuses.Unrestricted, null, [new AcceptReceiptLineRequest(grLine2Id, 6m)]));
        Assert.Equal(HttpStatusCode.Conflict, closedPostRes.StatusCode);

        // 3. Open period preview for 10 PCS shows projected (1,000,000 + 1,300,000) / 20 = 115,000 MAC & balanced GL
        var openPreviewRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}/accept-preview",
            new PreviewReceiptAcceptanceRequest(new DateOnly(2026, 11, 10), StockStatuses.Unrestricted, [new AcceptReceiptLineRequest(grLine2Id, 10m)]));
        Assert.Equal(HttpStatusCode.OK, openPreviewRes.StatusCode);
        var openPreview = await openPreviewRes.Content.ReadFromJsonAsync<AcceptancePreviewDto>(JsonOptions);
        Assert.True(openPreview!.CanPost);
        Assert.Equal(115_000m, openPreview.Lines[0].ProjectedMovingAverageCost);
        Assert.Equal(2_300_000m, openPreview.Lines[0].ProjectedCarryingValue);
        Assert.Equal(2, openPreview.JournalPreview.Count);

        // 4. Split Batch 2 acceptance: 6 PCS UNRESTRICTED (PARTIALLY_ACCEPTED) then 4 PCS QUALITY_INSPECTION (VALUED_ACCEPTED)
        var acc2aRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-acc-2a", new DateOnly(2026, 11, 10), StockStatuses.Unrestricted, "Partial Unrestricted", [new AcceptReceiptLineRequest(grLine2Id, 6m)]));
        Assert.Equal(HttpStatusCode.Created, acc2aRes.StatusCode);
        var acc2a = await acc2aRes.Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions);
        Assert.Equal(GoodsReceiptStatuses.PartiallyAccepted, acc2a!.ReceiptStatus);
        Assert.Equal(780_000m, acc2a.TotalAcceptedAmount);

        var acc2bRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-acc-2b", new DateOnly(2026, 11, 11), StockStatuses.QualityInspection, "QI Inspection", [new AcceptReceiptLineRequest(grLine2Id, 4m)]));
        Assert.Equal(HttpStatusCode.Created, acc2bRes.StatusCode);
        var acc2b = await acc2bRes.Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions);
        Assert.Equal(GoodsReceiptStatuses.ValuedAccepted, acc2b!.ReceiptStatus);
        Assert.Equal(520_000m, acc2b.TotalAcceptedAmount);

        // Verify receipt detail keeps Gate 1 LinkedGlJournalCount=0 while surfacing 2 ValuedAcceptances
        var grnDetail = await (await op.GetAsync($"{rcvBase}/receipts/{grn2.GoodsReceiptId:D}")).Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);
        Assert.Equal(0, grnDetail!.LinkedGlJournalCount);
        Assert.Equal(2, grnDetail.ValuedAcceptanceJournalCount);
        Assert.Equal(2, grnDetail.ValuedAcceptances.Count);

        // Verify inventory overview & item ledger
        var overview = await (await op.GetAsync($"{invBase}/overview")).Content.ReadFromJsonAsync<InventoryOverviewResponse>(JsonOptions);
        Assert.Equal(2_300_000m, overview!.TotalCarryingValueIdr);
        Assert.Equal(2_300_000m, overview.TotalOpenGrniAccrualIdr);
        Assert.Equal(1, overview.RestrictedStockItemCount);

        var ledger = await (await op.GetAsync($"{invBase}/items/{world.ItemStockAId:D}/ledger")).Content.ReadFromJsonAsync<ItemValuationLedgerResponse>(JsonOptions);
        Assert.Equal(20m, ledger!.CurrentValuedBaseQuantity);
        Assert.Equal(16m, ledger.UnrestrictedBaseQuantity);
        Assert.Equal(4m, ledger.QualityInspectionBaseQuantity);
        Assert.Equal(16m, ledger.AvailableBaseQuantity); // QI contributes to valued on-hand but 0 fulfillment availability
        Assert.Equal(2_300_000m, ledger.CurrentCarryingValueAmount);
        Assert.Equal(115_000m, ledger.CurrentMovingAverageUnitCost);
        Assert.Equal(3, ledger.Entries.Count);

        // 5. Inject runtime before-commit failure inside real PostgreSQL TX & verify zero partial rows survive
        var (po3, line3) = await CreateApprovedPoAsync(op, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 5m, 140_000m);
        var grn3 = await RecordArrivalAndReleaseAsync(op, rcvBase, po3.PurchaseOrderId, line3.PoLineId, world.PcsUomAId, "SJ-FAULT-03", "idem-gr3", 5m, 5m);

        using (var faultScope = factory.Services.CreateScope())
        {
            var scopeExecutor = faultScope.ServiceProvider.GetRequiredService<TenantScopeExecutor>();
            var db = faultScope.ServiceProvider.GetRequiredService<AtheraDbContext>();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await scopeExecutor.ExecuteInEntityAsync(
                    world.OperatorA1Id,
                    world.TenantAId,
                    world.EntityA1Id,
                    PermissionCodes.ReceivingAccept,
                    async (_, innerCt) =>
                    {
                        await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(
                            db,
                            new AcceptReceiptCommand(
                                world.TenantAId,
                                world.EntityA1Id,
                                "IDR",
                                grn3.GoodsReceiptId,
                                "idem-fault-hook",
                                new DateOnly(2026, 11, 12),
                                StockStatuses.Unrestricted,
                                null,
                                [new AcceptReceiptLineInput(grn3.Lines[0].GoodsReceiptLineId, 5m)],
                                world.OperatorA1Id,
                                DateTimeOffset.UtcNow),
                            innerCt,
                            beforeCommitFaultHook: _ => throw new InvalidOperationException("simulated_commit_crash"));
                        return true;
                    },
                    CancellationToken.None);
            });
        }

        // Verify zero orphan writes committed after fault
        var overviewAfterFault = await (await op.GetAsync($"{invBase}/overview")).Content.ReadFromJsonAsync<InventoryOverviewResponse>(JsonOptions);
        Assert.Equal(2_300_000m, overviewAfterFault!.TotalCarryingValueIdr);
        Assert.Equal(3, overviewAfterFault.RecentMovements.Count);
        Assert.Equal(3, overviewAfterFault.OpenAccruals.Count);
    }

    [Fact]
    public async Task S07_API_02_ConcurrentAcceptancesAndIdempotencyReplayNeverDoublePost()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var op = await CreateSignedInClientAsync(factory, world.OperatorA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);

        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var (po, poLine) = await CreateApprovedPoAsync(op, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 10m, 200_000m);
        var grn = await RecordArrivalAndReleaseAsync(op, rcvBase, po.PurchaseOrderId, poLine.PoLineId, world.PcsUomAId, "SJ-RACE-07", "idem-race-gr", 10m, 10m);
        var grLineId = grn.Lines[0].GoodsReceiptLineId;

        var reqA = new AcceptReceiptRequest("race-key-alpha", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, "Race A", [new AcceptReceiptLineRequest(grLineId, 10m)]);
        var reqB = new AcceptReceiptRequest("race-key-beta", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, "Race B", [new AcceptReceiptLineRequest(grLineId, 10m)]);

        var t1 = op.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", reqA);
        var t2 = op.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", reqB);
        var responses = await Task.WhenAll(t1, t2);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, createdCount);
        Assert.Equal(1, conflictCount);

        var winner = responses[0].StatusCode == HttpStatusCode.Created ? reqA : reqB;
        var winnerBody = await (responses[0].StatusCode == HttpStatusCode.Created ? responses[0] : responses[1])
            .Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions);

        // Replay winner key with identical payload -> 200 idempotent replay
        var replayRes = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", winner);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayBody = await replayRes.Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions);
        Assert.True(replayBody!.WasIdempotentReplay);
        Assert.Equal(winnerBody!.ValuedAcceptanceId, replayBody.ValuedAcceptanceId);

        // Replay winner key with mutated payload -> 409 duplicate_acceptance_idempotency_conflict
        var conflictReplay = await op.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", winner with { StockStatus = StockStatuses.Blocked });
        Assert.Equal(HttpStatusCode.Conflict, conflictReplay.StatusCode);
        Assert.Equal("duplicate_acceptance_idempotency_conflict", (await conflictReplay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task S07_API_03_PermissionSeparationHoldExclusionAndHostileSelectorsEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var op = await CreateSignedInClientAsync(factory, world.OperatorA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var dockOnly = await CreateSignedInClientAsync(factory, world.DockOnlyA1Email, world.Password);
        using var readerA2 = await CreateSignedInClientAsync(factory, world.ReaderA2Email, world.Password);

        var rcvA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var invA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";
        var rcvB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/receiving";
        var invB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/inventory";

        // Create receipt with 6 PCS released and 4 PCS held in DOCK_HELD
        var (po, poLine) = await CreateApprovedPoAsync(op, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, world.ItemStockAId, world.PcsUomAId, 10m, 100_000m);
        var grn = await RecordArrivalAndReleaseAsync(op, rcvA1, po.PurchaseOrderId, poLine.PoLineId, world.PcsUomAId, "SJ-HOLD-07", "idem-hold-gr", 10m, releasedQty: 6m);
        var grLineId = grn.Lines[0].GoodsReceiptLineId;

        // Dock clerk without receiving.accept gets 403 on POST /accept and 403 on /inventory/overview
        var dockAcceptRes = await dockOnly.PostAsJsonAsync($"{rcvA1}/receipts/{grn.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-deny-dock", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, null, [new AcceptReceiptLineRequest(grLineId, 6m)]));
        Assert.Equal(HttpStatusCode.Forbidden, dockAcceptRes.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await dockOnly.GetAsync($"{invA1}/overview")).StatusCode);

        // Read-only NUSA-SBY operator cannot post to NUSA-JKT (404 ungranted entity)
        var crossEntityRes = await readerA2.PostAsJsonAsync($"{rcvA1}/receipts/{grn.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-deny-entity", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, null, [new AcceptReceiptLineRequest(grLineId, 6m)]));
        Assert.Equal(HttpStatusCode.NotFound, crossEntityRes.StatusCode);

        // Hostile cross-tenant calls return 404 with zero leakage
        var crossInv = await op.GetAsync($"{invB1}/overview");
        Assert.Equal(HttpStatusCode.NotFound, crossInv.StatusCode);
        Assert.DoesNotContain("SMDR", await crossInv.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var crossAccept = await op.PostAsJsonAsync($"{rcvB1}/receipts/{world.SeedGrnB1Id:D}/accept",
            new AcceptReceiptRequest("idem-hostile-tenant", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, null, [new AcceptReceiptLineRequest(Guid.NewGuid(), 1m)]));
        Assert.Equal(HttpStatusCode.NotFound, crossAccept.StatusCode);

        // Attempting to accept 7 PCS (> 6 released; 4 is held) returns 409 acceptance_exceeds_released_quantity
        var overReleaseRes = await op.PostAsJsonAsync($"{rcvA1}/receipts/{grn.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest("idem-over-rel", new DateOnly(2026, 11, 12), StockStatuses.Unrestricted, null, [new AcceptReceiptLineRequest(grLineId, 7m)]));
        Assert.Equal(HttpStatusCode.Conflict, overReleaseRes.StatusCode);
        Assert.Equal("acceptance_exceeds_released_quantity", (await overReleaseRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());
    }

    private static async Task<(PurchaseOrderDetailDto Po, PurchaseOrderLineDto Line)> CreateApprovedPoAsync(
        HttpClient buyer,
        HttpClient approver,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal qty,
        decimal unitPrice)
    {
        var procBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/procurement";
        var createRes = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            supplierId, "IDR", new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 20), "Jakarta Main Warehouse", 30, null, null,
            [new PoLineInputDto("L001", itemId, uomId, qty, unitPrice, null)]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draft = await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft!.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null))).StatusCode);

        var decideRes = await approver.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest("APPROVED", "Approved for S07 test"));
        Assert.Equal(HttpStatusCode.OK, decideRes.StatusCode);
        var approved = await decideRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions);

        return (approved!, approved!.Revisions[0].Lines[0]);
    }

    private static async Task<GoodsReceiptDetailDto> RecordArrivalAndReleaseAsync(
        HttpClient client,
        string rcvBase,
        Guid poId,
        Guid poLineId,
        Guid uomId,
        string deliveryNote,
        string idempotencyKey,
        decimal deliveredQty,
        decimal releasedQty)
    {
        var arrRes = await client.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            poId,
            deliveryNote,
            idempotencyKey,
            new DateOnly(2026, 11, 8),
            "Main Dock",
            null,
            null,
            null,
            [new ArrivalLineRequest(poLineId, uomId, deliveredQty, true, DiscrepancyReasonCodes.None, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions);

        var heldQty = deliveredQty - releasedQty;
        var chkRes = await client.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn!.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                $"chk-{idempotencyKey}",
                0m,
                heldQty,
                releasedQty,
                0m,
                heldQty > 0m ? DiscrepancyReasonCodes.DamagedPackaging : DiscrepancyReasonCodes.None,
                "Released for S07 test",
                null));
        Assert.Equal(HttpStatusCode.OK, chkRes.StatusCode);
        return (await chkRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;
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

    private static async Task<S07SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (opA1Id, opA1Email, _) = await fx.CreateTestUserAsync("op.a1.s07@test.local", "Budi S07 Operator", pw);
        var (approverA1Id, approverA1Email, _) = await fx.CreateTestUserAsync("approver.a1.s07@test.local", "Rina PO Approver", pw);
        var (dockA1Id, dockA1Email, _) = await fx.CreateTestUserAsync("dock.a1.s07@test.local", "Agus Dock Clerk", pw);
        var (readerA2Id, readerA2Email, _) = await fx.CreateTestUserAsync("reader.a2.s07@test.local", "Andi Reader SBY", pw);
        var (foreignB1Id, foreignB1Email, _) = await fx.CreateTestUserAsync("op.b1.s07@test.local", "Samudra Operator", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-15);
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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, opA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approverA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, dockA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readerA2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignB1Id, now));

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept, PermissionCodes.InventoryRead
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, opA1Id, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverA1Id, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dockA1Id, perm, now, null, now));
        }

        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.WorkspaceView, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.ReceivingRead, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerA2Id, PermissionCodes.InventoryRead, now, null, now));

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage, PermissionCodes.ProcurementApprove,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept, PermissionCodes.InventoryRead
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignB1Id, perm, now, null, now));
        }

        // Single-approver false four-eyes in test helper for streamlined PO approval while keeping ApprovalPolicy required
        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", false, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantBId, entityB1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", false, false, now));

        db.DeliveryTolerancePolicies.AddRange(
            DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now),
            DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantBId, entityB1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));

        var invAccA1 = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var grniAccA1 = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        db.GlAccounts.AddRange(invAccA1, grniAccA1);

        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAccA1, "Inventory mapping", opA1Id, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grniAccA1, "GRNI mapping", opA1Id, now));

        var supA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-A", "PT Sumber Makmur", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var supB = Partner.Create(Guid.NewGuid(), tenantBId, "SUP-B", "CV Samudra Supply", "Samudra Supply", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(supA, supB);

        var pcsA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var pcsB = UnitOfMeasure.Create(Guid.NewGuid(), tenantBId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.AddRange(pcsA, pcsB);

        var itemA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-STL-PLATE-10", "Pelat Baja 10mm", ItemKinds.StockedGood, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemB = Item.Create(Guid.NewGuid(), tenantBId, "SMDR-ITM", "Palet Samudra", ItemKinds.StockedGood, pcsB.UomId, pcsB.UomId, pcsB.UomId, EntityScopeModes.AllEntities, now);
        db.Items.AddRange(itemA, itemB);
        await db.SaveChangesAsync();

        // Seed Batch 1 on EntityA1: 10 PCS @ IDR 100,000 -> ACC-2026-00001
        await using var seedTx = await db.Database.BeginTransactionAsync();
        var poA1 = await PurchaseOrderWorkflowService.CreateDraftAsync(db, new CreatePoDraftCommand(tenantAId, entityA1Id, "IDR", "IDR", supA.PartnerId, new DateOnly(2026, 11, 2), null, "Gudang A1", 30, null, null,
            [new PoLineInput("L001", itemA.ItemId, pcsA.UomId, 10m, 100_000m, null)], opA1Id, now), CancellationToken.None);
        Assert.True(poA1.Succeeded, $"{poA1.ErrorCode}: {poA1.ErrorDetail}");
        var subA1 = await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantAId, entityA1Id, poA1.PurchaseOrder!.PurchaseOrderId, poA1.Revision!.RevisionId, null, opA1Id, now), CancellationToken.None);
        Assert.True(subA1.Succeeded, $"{subA1.ErrorCode}: {subA1.ErrorDetail}");
        var decA1 = await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantAId, entityA1Id, poA1.PurchaseOrder.PurchaseOrderId, poA1.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded", dockA1Id, now), CancellationToken.None);
        Assert.True(decA1.Succeeded, $"{decA1.ErrorCode}: {decA1.ErrorDetail}");
        var poLineA1 = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == poA1.Revision.RevisionId);
        var grnA1 = await GoodsReceiptWorkflowService.RecordArrivalAsync(db, new RecordGoodsReceiptCommand(tenantAId, entityA1Id, poA1.PurchaseOrder.PurchaseOrderId, "SJ-A1-SEED", "idem-a1-grn", new DateOnly(2026, 11, 2), "Dock A1", null, null, null,
            [new ArrivalLineInput(poLineA1.PoLineId, pcsA.UomId, 10m, false, DiscrepancyReasonCodes.None, null, null)], opA1Id, now), CancellationToken.None);
        Assert.True(grnA1.Succeeded, $"{grnA1.ErrorCode}: {grnA1.ErrorDetail}");
        var grLineA1 = await db.GoodsReceiptLines.SingleAsync(l => l.GoodsReceiptId == grnA1.Receipt!.GoodsReceiptId);
        var dcA1 = await GoodsReceiptWorkflowService.RecordDockCheckAsync(db, new RecordDockCheckCommand(tenantAId, entityA1Id, grnA1.Receipt!.GoodsReceiptId, grLineA1.GoodsReceiptLineId, "idem-a1-dc", 0m, 0m, 10m, 0m, DiscrepancyReasonCodes.None, "Release 10", null, opA1Id, now), CancellationToken.None);
        Assert.True(dcA1.Succeeded, $"{dcA1.ErrorCode}: {dcA1.ErrorDetail}");
        var accA1 = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(db, new AcceptReceiptCommand(tenantAId, entityA1Id, "IDR", grnA1.Receipt.GoodsReceiptId, "idem-a1-acc", new DateOnly(2026, 11, 2), StockStatuses.Unrestricted, "Batch 1",
            [new AcceptReceiptLineInput(grLineA1.GoodsReceiptLineId, 10m)], opA1Id, now), CancellationToken.None);
        Assert.True(accA1.Succeeded, $"{accA1.ErrorCode}: {accA1.ErrorDetail}");

        // Seed Tenant B PO & Gate 1 receipt for hostile cross-tenant probing
        var (approverB1Id, _, _) = await fx.CreateTestUserAsync("approver.b1.s07@test.local", "Samudra Approver", pw);
        db.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), tenantBId, approverB1Id, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, approverB1Id, PermissionCodes.ProcurementApprove, now, null, now));
        await db.SaveChangesAsync();

        var poB1 = await PurchaseOrderWorkflowService.CreateDraftAsync(db, new CreatePoDraftCommand(tenantBId, entityB1Id, "IDR", "IDR", supB.PartnerId, new DateOnly(2026, 11, 2), null, "Gudang B1", 30, null, null,
            [new PoLineInput("L001", itemB.ItemId, pcsB.UomId, 10m, 10_000m, null)], foreignB1Id, now), CancellationToken.None);
        Assert.True(poB1.Succeeded, $"{poB1.ErrorCode}: {poB1.ErrorDetail}");
        var subB1 = await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantBId, entityB1Id, poB1.PurchaseOrder!.PurchaseOrderId, poB1.Revision!.RevisionId, null, foreignB1Id, now), CancellationToken.None);
        Assert.True(subB1.Succeeded, $"{subB1.ErrorCode}: {subB1.ErrorDetail}");
        var decB1 = await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantBId, entityB1Id, poB1.PurchaseOrder.PurchaseOrderId, poB1.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded B", approverB1Id, now), CancellationToken.None);
        Assert.True(decB1.Succeeded, $"{decB1.ErrorCode}: {decB1.ErrorDetail}");
        var poLineB1 = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == poB1.Revision.RevisionId);
        var grnB1 = await GoodsReceiptWorkflowService.RecordArrivalAsync(db, new RecordGoodsReceiptCommand(tenantBId, entityB1Id, poB1.PurchaseOrder.PurchaseOrderId, "SJ-B1-SEED", "idem-b1-grn", new DateOnly(2026, 11, 2), "Dock B1", null, null, null,
            [new ArrivalLineInput(poLineB1.PoLineId, pcsB.UomId, 10m, false, DiscrepancyReasonCodes.None, null, null)], foreignB1Id, now), CancellationToken.None);
        Assert.True(grnB1.Succeeded, $"{grnB1.ErrorCode}: {grnB1.ErrorDetail}");
        await seedTx.CommitAsync();

        return new S07SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            opA1Id, opA1Email, approverA1Id, approverA1Email, dockA1Id, dockA1Email, readerA2Id, readerA2Email, foreignB1Id, foreignB1Email,
            supA.PartnerId, supB.PartnerId, pcsA.UomId, itemA.ItemId, itemB.ItemId,
            accA1.Acceptance!.ValuedAcceptanceId, poA1.PurchaseOrder.PurchaseOrderId, grnB1.Receipt!.GoodsReceiptId, pw);
    }
}
