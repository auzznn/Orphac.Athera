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
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Quality;

namespace Orphac.Athera.IntegrationTests;

public sealed class S08StockAvailabilityPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S08SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ClerkA1Id,
        string ClerkA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid InspectorA1Id,
        string InspectorA1Email,
        Guid ForeignB1Id,
        string ForeignB1Email,
        Guid SupplierAId,
        Guid PcsUomAId,
        Guid ItemUnrestrictedAId,
        Guid ItemQuarantineAId,
        Guid SeedInspectionAId,
        Guid SeedDispositionAId,
        Guid SeedBalanceUnrestrictedAId,
        Guid DefaultWarehouseA1Id,
        Guid LocMainA1Id,
        Guid LocQcA1Id,
        Guid LocBlkA1Id,
        string Password);

    [Fact]
    public async Task S08_PG_01_RlsAppendOnlyAndCheckConstraintsEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. All 9 S08 tables enforce ENABLE + FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'inventory' AND c.relname IN (
                        'warehouses', 'locations', 'stock_lots', 'stock_location_balances', 'reservation_sequences', 'stock_reservations'))
                    OR (n.nspname = 'quality' AND c.relname IN (
                        'inspection_sequences', 'receipt_inspections', 'inspection_dispositions'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(9L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id denies reads (0 rows) and writes (42501)
        foreach (var table in new[]
        {
            "inventory.warehouses",
            "inventory.locations",
            "inventory.stock_lots",
            "inventory.stock_location_balances",
            "inventory.stock_reservations",
            "quality.receipt_inspections",
            "quality.inspection_dispositions"
        })
        {
            await using var readCmd = conn.CreateCommand();
            readCmd.CommandText = $"SELECT count(*) FROM {table};";
            Assert.Equal(0L, Convert.ToInt64(await readCmd.ExecuteScalarAsync()));
        }

        await using (var noCtxWrite = conn.CreateCommand())
        {
            noCtxWrite.CommandText = """
                INSERT INTO inventory.warehouses (warehouse_id, tenant_id, legal_entity_id, warehouse_code, name, is_default, status, created_at_utc, updated_at_utc, version)
                VALUES (@wid, @tid, @eid, 'WH-DENY', 'Unauthorized Warehouse', false, 'ACTIVE', now(), now(), 1);
                """;
            noCtxWrite.Parameters.AddWithValue("wid", Guid.NewGuid());
            noCtxWrite.Parameters.AddWithValue("tid", world.TenantBId);
            noCtxWrite.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => noCtxWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Under Tenant B context: Tenant A data invisible & cross-tenant write rejected with 42501
        await using (var txB = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, txB, world.TenantBId);

            foreach (var table in new[] { "inventory.warehouses", "inventory.stock_location_balances", "quality.receipt_inspections" })
            {
                await using var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = txB;
                checkCmd.CommandText = $"SELECT count(*) FROM {table} WHERE tenant_id = @ta;";
                checkCmd.Parameters.AddWithValue("ta", world.TenantAId);
                Assert.Equal(0L, Convert.ToInt64(await checkCmd.ExecuteScalarAsync()));
            }

            await using var crossWrite = conn.CreateCommand();
            crossWrite.Transaction = txB;
            crossWrite.CommandText = """
                INSERT INTO inventory.warehouses (warehouse_id, tenant_id, legal_entity_id, warehouse_code, name, is_default, status, created_at_utc, updated_at_utc, version)
                VALUES (@wid, @ta, @ea, 'WH-CROSS', 'Cross Tenant Warehouse', false, 'ACTIVE', now(), now(), 1);
                """;
            crossWrite.Parameters.AddWithValue("wid", Guid.NewGuid());
            crossWrite.Parameters.AddWithValue("ta", world.TenantAId);
            crossWrite.Parameters.AddWithValue("ea", world.EntityA1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => crossWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. Append-only inspection_dispositions & non-deletable S08 tables reject mutations with 42501; CHECKs enforce 23514
        await using (var txA = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, txA, world.TenantAId);

            var privStatements = new[]
            {
                $"UPDATE quality.inspection_dispositions SET notes = 'tamper' WHERE inspection_disposition_id = '{world.SeedDispositionAId:D}';",
                $"DELETE FROM quality.inspection_dispositions WHERE inspection_disposition_id = '{world.SeedDispositionAId:D}';",
                $"DELETE FROM inventory.stock_location_balances WHERE stock_location_balance_id = '{world.SeedBalanceUnrestrictedAId:D}';",
                $"DELETE FROM inventory.stock_reservations WHERE tenant_id = '{world.TenantAId:D}';",
                $"DELETE FROM quality.receipt_inspections WHERE receipt_inspection_id = '{world.SeedInspectionAId:D}';"
            };

            foreach (var sql in privStatements)
            {
                await using var sp = conn.CreateCommand();
                sp.Transaction = txA;
                sp.CommandText = "SAVEPOINT s_priv;";
                await sp.ExecuteNonQueryAsync();

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = txA;
                cmd.CommandText = sql;
                var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
                Assert.Equal("42501", ex.SqlState);

                await using var rb = conn.CreateCommand();
                rb.Transaction = txA;
                rb.CommandText = "ROLLBACK TO SAVEPOINT s_priv;";
                await rb.ExecuteNonQueryAsync();
            }

            // CHECK constraint 1: negative on_hand_base_quantity
            await AssertCheckViolationAsync(conn, txA, $"""
                UPDATE inventory.stock_location_balances
                SET on_hand_base_quantity = -1
                WHERE stock_location_balance_id = '{world.SeedBalanceUnrestrictedAId:D}';
                """);

            // CHECK constraint 2: reserved > on_hand on UNRESTRICTED balance
            await AssertCheckViolationAsync(conn, txA, $"""
                UPDATE inventory.stock_location_balances
                SET reserved_base_quantity = 999, available_base_quantity = 0
                WHERE stock_location_balance_id = '{world.SeedBalanceUnrestrictedAId:D}';
                """);

            // CHECK constraint 3: released + blocked + pending <> inspected_total on receipt_inspections
            await AssertCheckViolationAsync(conn, txA, $"""
                UPDATE quality.receipt_inspections
                SET pending_base_quantity = pending_base_quantity + 5
                WHERE receipt_inspection_id = '{world.SeedInspectionAId:D}';
                """);
        }
    }

    [Fact]
    public async Task S08_PG_02_MigrationM08DownToM07AndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261118005753_M07_ValuedAcceptanceMovingAverageInventoryAndGrni");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = """
                SELECT count(*)
                FROM information_schema.tables
                WHERE table_schema = 'quality'
                   OR (table_schema = 'inventory' AND table_name IN (
                        'warehouses', 'locations', 'stock_lots', 'stock_location_balances', 'reservation_sequences', 'stock_reservations'));
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));

            await using var colCheck = conn.CreateCommand();
            colCheck.CommandText = """
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = 'catalog' AND table_name = 'items' AND column_name = 'lot_tracking_policy';
                """;
            Assert.Equal(0L, Convert.ToInt64(await colCheck.ExecuteScalarAsync()));
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
                    (n.nspname = 'inventory' AND c.relname IN (
                        'warehouses', 'locations', 'stock_lots', 'stock_location_balances', 'reservation_sequences', 'stock_reservations'))
                    OR (n.nspname = 'quality' AND c.relname IN (
                        'inspection_sequences', 'receipt_inspections', 'inspection_dispositions'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(9L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S08_API_01_QuarantineDispositionAndLocationTransferConserveStockAndValuation()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var clerk = await CreateSignedInClientAsync(factory, world.ClerkA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var inspector = await CreateSignedInClientAsync(factory, world.InspectorA1Email, world.Password);

        var catBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/catalog";
        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var invBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";

        // 1. Create REQUIRED lot-tracked stocked item via Clerk
        var itemRes = await clerk.PostAsJsonAsync($"{catBase}/items", new CreateItemRequest(
            "ITM-S08-LOT",
            "Reagen Pelarut Uji Lot",
            ItemKinds.StockedGood,
            world.PcsUomAId,
            world.PcsUomAId,
            world.PcsUomAId,
            EntityScopeModes.AllEntities,
            LotTrackingPolicies.Required));
        Assert.Equal(HttpStatusCode.Created, itemRes.StatusCode);
        var createdItem = (await itemRes.Content.ReadFromJsonAsync<ItemDetailDto>(JsonOptions))!;
        Assert.Equal(LotTrackingPolicies.Required, createdItem.LotTrackingPolicy);

        // 2. Approve PO (20 PCS @ 250,000 IDR = 5,000,000 IDR) & release Gate 1
        var (po, poLine) = await CreateApprovedPoAsync(clerk, approver, world.TenantAId, world.EntityA1Id, world.SupplierAId, createdItem.ItemId, world.PcsUomAId, 20m, 250_000m);
        var grn = await RecordArrivalAndReleaseAsync(clerk, rcvBase, po.PurchaseOrderId, poLine.PoLineId, world.PcsUomAId, "SJ-S08-LOT-01", "idem-s08-gr1", 20m, 20m);
        var grLineId = grn.Lines[0].GoodsReceiptLineId;

        // 3. Missing lot number blocked on REQUIRED item
        var missingLotRes = await clerk.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            "idem-s08-nolot",
            new DateOnly(2026, 11, 10),
            StockStatuses.QualityInspection,
            "No lot test",
            [new AcceptReceiptLineRequest(grLineId, 20m)]));
        Assert.Equal(HttpStatusCode.Conflict, missingLotRes.StatusCode);
        var missingLotBody = await missingLotRes.Content.ReadAsStringAsync();
        Assert.Contains("lot_required_by_item_policy", missingLotBody, StringComparison.Ordinal);

        // 4. Gate 2 acceptance into QUALITY_INSPECTION with LOT-S08-A1 succeeds (201)
        var acceptRes = await clerk.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            "idem-s08-acc-lot",
            new DateOnly(2026, 11, 10),
            StockStatuses.QualityInspection,
            "Quarantine lot receipt",
            [new AcceptReceiptLineRequest(grLineId, 20m, null, "LOT-S08-A1", "SUP-S08-01", new DateOnly(2026, 11, 1), new DateOnly(2027, 11, 30))]));
        Assert.Equal(HttpStatusCode.Created, acceptRes.StatusCode);

        // Capture GL journal & valuation pool counts right after Gate 2
        long glCountBefore;
        long valEntryCountBefore;
        decimal carryingBefore;
        decimal macBefore;
        await using (var checkDb = fx.CreateMigratorDbContext())
        {
            glCountBefore = await checkDb.JournalEntries.CountAsync(j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id);
            valEntryCountBefore = await checkDb.ValuationEntries.CountAsync(e => e.TenantId == world.TenantAId && e.LegalEntityId == world.EntityA1Id && e.ItemId == createdItem.ItemId);
            var pool = await checkDb.ValuationPools.SingleAsync(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.ItemId == createdItem.ItemId);
            carryingBefore = pool.CarryingValueAmount;
            macBefore = pool.MovingAverageUnitCost;
        }
        Assert.Equal(5_000_000m, carryingBefore);
        Assert.Equal(250_000m, macBefore);

        // 5. Inspect warehouses & create secondary LOC-QC-B (QUARANTINE) + LOC-RACK-C1 (STORAGE)
        var whRes = await (await clerk.GetAsync($"{invBase}/warehouses")).Content.ReadFromJsonAsync<WarehouseListResponse>(JsonOptions);
        var locQcBRes = await clerk.PostAsJsonAsync($"{invBase}/warehouses/{world.DefaultWarehouseA1Id:D}/locations",
            new CreateWarehouseLocationRequest("LOC-QC-B", "Zona Karantina Sekunder", LocationZoneTypes.Quarantine, false));
        Assert.Equal(HttpStatusCode.Created, locQcBRes.StatusCode);
        var locQcB = (await locQcBRes.Content.ReadFromJsonAsync<WarehouseLocationDto>(JsonOptions))!;

        var locRackC1Res = await clerk.PostAsJsonAsync($"{invBase}/warehouses/{world.DefaultWarehouseA1Id:D}/locations",
            new CreateWarehouseLocationRequest("LOC-RACK-C1", "Rak Penyimpanan C1", LocationZoneTypes.Storage, false));
        Assert.Equal(HttpStatusCode.Created, locRackC1Res.StatusCode);
        var locRackC1 = (await locRackC1Res.Content.ReadFromJsonAsync<WarehouseLocationDto>(JsonOptions))!;

        var detailInitial = (await (await clerk.GetAsync($"{invBase}/items/{createdItem.ItemId:D}/detail")).Content.ReadFromJsonAsync<ItemStockDetailResponse>(JsonOptions))!;
        Assert.Single(detailInitial.Inspections);
        var inspection = detailInitial.Inspections[0];
        var lotId = detailInitial.Lots.Single().StockLotId;
        Assert.Equal(ReceiptInspectionStatuses.Pending, inspection.Status);

        // 6. Transfer out of LOC-QC while pending inspection is locked
        var lockedTransferRes = await clerk.PostAsJsonAsync($"{invBase}/transfers", new TransferStockLocationRequest(
            createdItem.ItemId,
            world.LocQcA1Id,
            locQcB.LocationId,
            lotId,
            StockStatuses.QualityInspection,
            5m,
            new DateOnly(2026, 11, 11),
            "idem-s08-xfer-locked",
            "Should fail while pending inspection"));
        Assert.Equal(HttpStatusCode.Conflict, lockedTransferRes.StatusCode);
        Assert.Contains("pending_receipt_inspection_location_locked", await lockedTransferRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Zone mismatch (QUALITY_INSPECTION into STORAGE) also rejected 409
        var zoneMismatchRes = await clerk.PostAsJsonAsync($"{invBase}/transfers", new TransferStockLocationRequest(
            createdItem.ItemId,
            world.LocQcA1Id,
            world.LocMainA1Id,
            lotId,
            StockStatuses.QualityInspection,
            5m,
            new DateOnly(2026, 11, 11),
            "idem-s08-xfer-zone",
            null));
        Assert.Equal(HttpStatusCode.Conflict, zoneMismatchRes.StatusCode);
        Assert.Contains("location_zone_mismatch", await zoneMismatchRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // 7. Inspector records partial split disposition: 12 UNRESTRICTED (LOC-MAIN) + 3 BLOCKED (LOC-BLK)
        var partialDispRes = await inspector.PostAsJsonAsync(
            $"{invBase}/inspections/{inspection.ReceiptInspectionId:D}/dispositions",
            new RecordInspectionDispositionRequest(
                new DateOnly(2026, 11, 11),
                12m,
                world.LocMainA1Id,
                3m,
                world.LocBlkA1Id,
                QualityReasonCodes.DamagedOrContaminated,
                "COA-JKT-01",
                "12 pass QC, 3 drum penyok",
                "idem-s08-disp-1"));
        Assert.Equal(HttpStatusCode.Created, partialDispRes.StatusCode);
        var partialOutcome = (await partialDispRes.Content.ReadFromJsonAsync<InspectionDispositionOutcomeDto>(JsonOptions))!;
        Assert.Equal(ReceiptInspectionStatuses.PartiallyDecided, partialOutcome.Inspection.Status);
        Assert.Equal(5m, partialOutcome.Inspection.PendingBaseQuantity);

        // Final disposition: release remaining 5 UNRESTRICTED to LOC-MAIN
        var finalDispRes = await inspector.PostAsJsonAsync(
            $"{invBase}/inspections/{inspection.ReceiptInspectionId:D}/dispositions",
            new RecordInspectionDispositionRequest(
                new DateOnly(2026, 11, 11),
                5m,
                world.LocMainA1Id,
                0m,
                null,
                QualityReasonCodes.PassedSpecification,
                "COA-JKT-02",
                "Sisa 5 PCS lulus QC",
                "idem-s08-disp-2"));
        Assert.Equal(HttpStatusCode.Created, finalDispRes.StatusCode);
        var finalOutcome = (await finalDispRes.Content.ReadFromJsonAsync<InspectionDispositionOutcomeDto>(JsonOptions))!;
        Assert.Equal(ReceiptInspectionStatuses.Completed, finalOutcome.Inspection.Status);
        Assert.Equal(0m, finalOutcome.Inspection.PendingBaseQuantity);

        // 8. Clerk transfers 4 UNRESTRICTED from LOC-MAIN -> LOC-RACK-C1
        var transferRes = await clerk.PostAsJsonAsync($"{invBase}/transfers", new TransferStockLocationRequest(
            createdItem.ItemId,
            world.LocMainA1Id,
            locRackC1.LocationId,
            lotId,
            StockStatuses.Unrestricted,
            4m,
            new DateOnly(2026, 11, 11),
            "idem-s08-xfer-ok",
            "Putaway to Rack C1"));
        Assert.Equal(HttpStatusCode.Created, transferRes.StatusCode);

        // 9. Assert valuation, MAC, and GL untouched; reconciliation = true across all buckets
        await using (var afterDb = fx.CreateMigratorDbContext())
        {
            var glCountAfter = await afterDb.JournalEntries.CountAsync(j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id);
            var valEntryCountAfter = await afterDb.ValuationEntries.CountAsync(e => e.TenantId == world.TenantAId && e.LegalEntityId == world.EntityA1Id && e.ItemId == createdItem.ItemId);
            var poolAfter = await afterDb.ValuationPools.SingleAsync(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.ItemId == createdItem.ItemId);

            Assert.Equal(glCountBefore, glCountAfter);
            Assert.Equal(valEntryCountBefore, valEntryCountAfter);
            Assert.Equal(carryingBefore, poolAfter.CarryingValueAmount);
            Assert.Equal(macBefore, poolAfter.MovingAverageUnitCost);
        }

        var detailAfter = (await (await clerk.GetAsync($"{invBase}/items/{createdItem.ItemId:D}/detail")).Content.ReadFromJsonAsync<ItemStockDetailResponse>(JsonOptions))!;
        Assert.True(detailAfter.Reconciliation.IsReconciled);
        Assert.Equal(20m, detailAfter.Reconciliation.PositionOnHandTotal);
        Assert.Equal(17m, detailAfter.Reconciliation.PositionUnrestricted);
        Assert.Equal(0m, detailAfter.Reconciliation.PositionQualityInspection);
        Assert.Equal(3m, detailAfter.Reconciliation.PositionBlocked);
        Assert.Equal(17m, detailAfter.Reconciliation.PositionAvailable);
        Assert.Equal(13m, detailAfter.LocationBalances.Single(b => b.LocationId == world.LocMainA1Id && b.StockStatus == StockStatuses.Unrestricted).OnHandBaseQuantity);
        Assert.Equal(4m, detailAfter.LocationBalances.Single(b => b.LocationId == locRackC1.LocationId && b.StockStatus == StockStatuses.Unrestricted).OnHandBaseQuantity);
        Assert.Equal(3m, detailAfter.LocationBalances.Single(b => b.LocationId == world.LocBlkA1Id && b.StockStatus == StockStatuses.Blocked).OnHandBaseQuantity);
    }

    [Fact]
    public async Task S08_API_02_ConcurrentReservationRaceIdempotentReplayAndRelease()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var clerk1 = await CreateSignedInClientAsync(factory, world.ClerkA1Email, world.Password);
        using var clerk2 = await CreateSignedInClientAsync(factory, world.ClerkA1Email, world.Password);

        var invBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";

        // Fire two concurrent reservations for 7 each against 10 UNRESTRICTED at LOC-MAIN
        var req1 = new CreateStockReservationRequest(
            world.ItemUnrestrictedAId, world.LocMainA1Id, null, ReservationSourceTypes.SalesOrder,
            null, "SO-RACE-01", null, 7m, null, "idem-race-1", "Thread 1");
        var req2 = new CreateStockReservationRequest(
            world.ItemUnrestrictedAId, world.LocMainA1Id, null, ReservationSourceTypes.SalesOrder,
            null, "SO-RACE-02", null, 7m, null, "idem-race-2", "Thread 2");

        var t1 = clerk1.PostAsJsonAsync($"{invBase}/reservations", req1);
        var t2 = clerk2.PostAsJsonAsync($"{invBase}/reservations", req2);
        var responses = await Task.WhenAll(t1, t2);

        var createdList = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        var conflictList = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Single(createdList);
        Assert.Single(conflictList);

        var conflictText = await conflictList[0].Content.ReadAsStringAsync();
        Assert.Contains("od10_insufficient_available_stock", conflictText, StringComparison.Ordinal);

        var winnerPayload = (await createdList[0].Content.ReadFromJsonAsync<StockReservationOutcomeDto>(JsonOptions))!;
        var winnerReq = ReferenceEquals(createdList[0], responses[0]) ? req1 : req2;
        Assert.Equal(7m, winnerPayload.Reservation.RemainingReservedBaseQuantity);

        // Idempotent replay with same key & payload returns 200 OK
        var replayRes = await clerk1.PostAsJsonAsync($"{invBase}/reservations", winnerReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayPayload = (await replayRes.Content.ReadFromJsonAsync<StockReservationOutcomeDto>(JsonOptions))!;
        Assert.True(replayPayload.WasIdempotentReplay);
        Assert.Equal(winnerPayload.Reservation.ReservationNumber, replayPayload.Reservation.ReservationNumber);

        // Idempotency key reuse with changed quantity returns 409 Conflict
        var mismatchReq = winnerReq with { Quantity = 6m };
        var mismatchRes = await clerk1.PostAsJsonAsync($"{invBase}/reservations", mismatchReq);
        Assert.Equal(HttpStatusCode.Conflict, mismatchRes.StatusCode);

        // Verify mid-state: Reserved = 7, Available = 3
        var detailMid = (await (await clerk1.GetAsync($"{invBase}/items/{world.ItemUnrestrictedAId:D}/detail")).Content.ReadFromJsonAsync<ItemStockDetailResponse>(JsonOptions))!;
        Assert.True(detailMid.Reconciliation.IsReconciled);
        Assert.Equal(7m, detailMid.Reconciliation.PositionReserved);
        Assert.Equal(3m, detailMid.Reconciliation.PositionAvailable);

        // Partial release 3 -> Remaining = 4, Available = 6
        var partialRelRes = await clerk1.PostAsJsonAsync(
            $"{invBase}/reservations/{winnerPayload.Reservation.StockReservationId:D}/release",
            new ReleaseStockReservationRequest(3m));
        Assert.Equal(HttpStatusCode.OK, partialRelRes.StatusCode);
        var partialRel = (await partialRelRes.Content.ReadFromJsonAsync<StockReservationOutcomeDto>(JsonOptions))!;
        Assert.Equal(4m, partialRel.Reservation.RemainingReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.Active, partialRel.Reservation.Status);

        // Full release remaining -> RELEASED, Available = 10
        var fullRelRes = await clerk1.PostAsJsonAsync(
            $"{invBase}/reservations/{winnerPayload.Reservation.StockReservationId:D}/release",
            new ReleaseStockReservationRequest(null));
        Assert.Equal(HttpStatusCode.OK, fullRelRes.StatusCode);
        var fullRel = (await fullRelRes.Content.ReadFromJsonAsync<StockReservationOutcomeDto>(JsonOptions))!;
        Assert.Equal(0m, fullRel.Reservation.RemainingReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.Released, fullRel.Reservation.Status);

        var detailFinal = (await (await clerk1.GetAsync($"{invBase}/items/{world.ItemUnrestrictedAId:D}/detail")).Content.ReadFromJsonAsync<ItemStockDetailResponse>(JsonOptions))!;
        Assert.True(detailFinal.Reconciliation.IsReconciled);
        Assert.Equal(0m, detailFinal.Reconciliation.PositionReserved);
        Assert.Equal(10m, detailFinal.Reconciliation.PositionAvailable);
    }

    [Fact]
    public async Task S08_API_03_PermissionSegregationAndHostileCrossScopeDenials()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        // Grant Clerk InventoryManage on EntityA2 solely to probe cross-entity ID smuggling on EntityA2 routes
        await using (var db = fx.CreateMigratorDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), world.TenantAId, world.EntityA2Id, world.ClerkA1Id, PermissionCodes.WorkspaceView, now, null, now));
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), world.TenantAId, world.EntityA2Id, world.ClerkA1Id, PermissionCodes.InventoryManage, now, null, now));
            await db.SaveChangesAsync();
        }

        using var factory = fx.CreateApiFactory();
        using var clerk = await CreateSignedInClientAsync(factory, world.ClerkA1Email, world.Password);
        using var inspector = await CreateSignedInClientAsync(factory, world.InspectorA1Email, world.Password);

        var invA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";
        var invA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/inventory";
        var invB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/inventory";

        // 1. Clerk lacks quality.inspect -> 403 on inspection disposition & status transitions
        var clerkDisp = await clerk.PostAsJsonAsync(
            $"{invA1}/inspections/{world.SeedInspectionAId:D}/dispositions",
            new RecordInspectionDispositionRequest(new DateOnly(2026, 11, 11), 2m, world.LocMainA1Id, 0m, null, QualityReasonCodes.PassedSpecification, null, null, "idem-sod-1"));
        Assert.Equal(HttpStatusCode.Forbidden, clerkDisp.StatusCode);

        var clerkHold = await clerk.PostAsJsonAsync(
            $"{invA1}/status-transitions",
            new StockStatusTransitionRequest(world.ItemUnrestrictedAId, world.LocMainA1Id, world.LocBlkA1Id, null, StockStatuses.Unrestricted, StockStatuses.Blocked, 1m, new DateOnly(2026, 11, 11), QualityReasonCodes.SpecDeviationHold, null, null, "idem-sod-2"));
        Assert.Equal(HttpStatusCode.Forbidden, clerkHold.StatusCode);

        // 2. Inspector lacks inventory.manage -> 403 on location transfers & reservations
        var inspTransfer = await inspector.PostAsJsonAsync(
            $"{invA1}/transfers",
            new TransferStockLocationRequest(world.ItemUnrestrictedAId, world.LocMainA1Id, world.LocMainA1Id, null, StockStatuses.Unrestricted, 1m, new DateOnly(2026, 11, 11), "idem-sod-3", null));
        Assert.Equal(HttpStatusCode.Forbidden, inspTransfer.StatusCode);

        var inspReserve = await inspector.PostAsJsonAsync(
            $"{invA1}/reservations",
            new CreateStockReservationRequest(world.ItemUnrestrictedAId, world.LocMainA1Id, null, ReservationSourceTypes.ManualHold, null, "HOLD-SOD", null, 1m, null, "idem-sod-4", null));
        Assert.Equal(HttpStatusCode.Forbidden, inspReserve.StatusCode);

        // 3. Hostile cross-tenant access fails (403/404) with zero foreign leakage
        var crossTenantRes = await clerk.GetAsync($"{invB1}/warehouses");
        Assert.True(crossTenantRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);

        // 4. Hostile cross-entity ID smuggling on EntityA2 using EntityA1 location & item returns 404
        var crossEntityReserve = await clerk.PostAsJsonAsync(
            $"{invA2}/reservations",
            new CreateStockReservationRequest(world.ItemUnrestrictedAId, world.LocMainA1Id, null, ReservationSourceTypes.ManualHold, null, "HOLD-SMUGGLE", null, 1m, null, "idem-cross-ent-1", null));
        Assert.Equal(HttpStatusCode.NotFound, crossEntityReserve.StatusCode);
    }

    private static async Task AssertCheckViolationAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
    {
        await using var sp = conn.CreateCommand();
        sp.Transaction = tx;
        sp.CommandText = "SAVEPOINT s_chk;";
        await sp.ExecuteNonQueryAsync();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", ex.SqlState);

        await using var rb = conn.CreateCommand();
        rb.Transaction = tx;
        rb.CommandText = "ROLLBACK TO SAVEPOINT s_chk;";
        await rb.ExecuteNonQueryAsync();
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
            new DecidePoRevisionRequest("APPROVED", "Approved for S08 test"));
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

        var chkRes = await client.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn!.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                $"chk-{idempotencyKey}",
                0m,
                0m,
                releasedQty,
                0m,
                DiscrepancyReasonCodes.None,
                "Released for S08 test",
                null));
        Assert.Equal(HttpStatusCode.OK, chkRes.StatusCode);
        return (await chkRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;
    }

    private static async Task<S08SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (clerkId, clerkEmail, _) = await fx.CreateTestUserAsync("clerk.s08@test.local", "Budi S08 Clerk", pw);
        var (approverId, approverEmail, _) = await fx.CreateTestUserAsync("approver.s08@test.local", "Rina PO Approver", pw);
        var (inspectorId, inspectorEmail, _) = await fx.CreateTestUserAsync("qc.s08@test.local", "Dian QC Inspector", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.s08@test.local", "Samudra Staff", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-20);
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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, clerkId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approverId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, inspectorId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.CatalogManage,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept,
            PermissionCodes.InventoryRead, PermissionCodes.InventoryManage
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, clerkId, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.ReceivingRead, PermissionCodes.InventoryRead, PermissionCodes.QualityInspect })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, inspectorId, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, perm, now, null, now));
        }

        db.ApprovalPolicies.Add(ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", false, false, now));
        db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));

        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var grniAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        db.GlAccounts.AddRange(invAcc, grniAcc);
        db.FinancialPeriods.Add(FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));
        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcc, "Inv", clerkId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grniAcc, "GRNI", clerkId, now));

        var supA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-A", "PT Sumber Makmur", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        db.Partners.Add(supA);

        var pcsA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcsA);

        var itemUnr = Item.Create(Guid.NewGuid(), tenantAId, "ITM-UNR-01", "Pelat Baja S08", ItemKinds.StockedGood, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemQi = Item.Create(Guid.NewGuid(), tenantAId, "ITM-QI-01", "Katup Tekanan Tinggi", ItemKinds.StockedGood, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now, LotTrackingPolicies.Optional);
        db.Items.AddRange(itemUnr, itemQi);
        await db.SaveChangesAsync();

        await using var seedTx = await db.Database.BeginTransactionAsync();
        var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);

        // Seed 10 UNRESTRICTED at LOC-MAIN for concurrent reservation race & SoD checks
        var po1 = await PurchaseOrderWorkflowService.CreateDraftAsync(db, new CreatePoDraftCommand(tenantAId, entityA1Id, "IDR", "IDR", supA.PartnerId, new DateOnly(2026, 11, 2), null, "Gudang A1", 30, null, null,
            [new PoLineInput("L001", itemUnr.ItemId, pcsA.UomId, 10m, 100_000m, null)], clerkId, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantAId, entityA1Id, po1.PurchaseOrder!.PurchaseOrderId, po1.Revision!.RevisionId, null, clerkId, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantAId, entityA1Id, po1.PurchaseOrder.PurchaseOrderId, po1.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded", approverId, now), CancellationToken.None);
        var poLine1 = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == po1.Revision.RevisionId);
        var grn1 = await GoodsReceiptWorkflowService.RecordArrivalAsync(db, new RecordGoodsReceiptCommand(tenantAId, entityA1Id, po1.PurchaseOrder.PurchaseOrderId, "SJ-S08-SEED1", "idem-s08-seed1-gr", new DateOnly(2026, 11, 2), "Dock A1", null, null, null,
            [new ArrivalLineInput(poLine1.PoLineId, pcsA.UomId, 10m, false, DiscrepancyReasonCodes.None, null, null)], clerkId, now), CancellationToken.None);
        var grLine1 = await db.GoodsReceiptLines.SingleAsync(l => l.GoodsReceiptId == grn1.Receipt!.GoodsReceiptId);
        await GoodsReceiptWorkflowService.RecordDockCheckAsync(db, new RecordDockCheckCommand(tenantAId, entityA1Id, grn1.Receipt!.GoodsReceiptId, grLine1.GoodsReceiptLineId, "idem-s08-seed1-dc", 0m, 0m, 10m, 0m, DiscrepancyReasonCodes.None, "OK", null, clerkId, now), CancellationToken.None);
        var acc1 = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(db, new AcceptReceiptCommand(tenantAId, entityA1Id, "IDR", grn1.Receipt.GoodsReceiptId, "idem-s08-seed1-acc", new DateOnly(2026, 11, 3), StockStatuses.Unrestricted, "Seed UNR",
            [new AcceptReceiptLineInput(grLine1.GoodsReceiptLineId, 10m)], clerkId, now), CancellationToken.None);
        Assert.True(acc1.Succeeded, $"{acc1.ErrorCode}: {acc1.ErrorDetail}");

        // Seed 10 QUALITY_INSPECTION at LOC-QC + initial disposition of 2 so inspection_dispositions has a seeded row for RLS/42501 checks
        var po2 = await PurchaseOrderWorkflowService.CreateDraftAsync(db, new CreatePoDraftCommand(tenantAId, entityA1Id, "IDR", "IDR", supA.PartnerId, new DateOnly(2026, 11, 2), null, "Gudang A1", 30, null, null,
            [new PoLineInput("L001", itemQi.ItemId, pcsA.UomId, 10m, 150_000m, null)], clerkId, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantAId, entityA1Id, po2.PurchaseOrder!.PurchaseOrderId, po2.Revision!.RevisionId, null, clerkId, now), CancellationToken.None);
        await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantAId, entityA1Id, po2.PurchaseOrder.PurchaseOrderId, po2.Revision.RevisionId, ApprovalDecisions.Approved, "Seeded QI", approverId, now), CancellationToken.None);
        var poLine2 = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == po2.Revision.RevisionId);
        var grn2 = await GoodsReceiptWorkflowService.RecordArrivalAsync(db, new RecordGoodsReceiptCommand(tenantAId, entityA1Id, po2.PurchaseOrder.PurchaseOrderId, "SJ-S08-SEED2", "idem-s08-seed2-gr", new DateOnly(2026, 11, 2), "Dock A1", null, null, null,
            [new ArrivalLineInput(poLine2.PoLineId, pcsA.UomId, 10m, false, DiscrepancyReasonCodes.None, null, null)], clerkId, now), CancellationToken.None);
        var grLine2 = await db.GoodsReceiptLines.SingleAsync(l => l.GoodsReceiptId == grn2.Receipt!.GoodsReceiptId);
        await GoodsReceiptWorkflowService.RecordDockCheckAsync(db, new RecordDockCheckCommand(tenantAId, entityA1Id, grn2.Receipt!.GoodsReceiptId, grLine2.GoodsReceiptLineId, "idem-s08-seed2-dc", 0m, 0m, 10m, 0m, DiscrepancyReasonCodes.None, "OK", null, clerkId, now), CancellationToken.None);
        var acc2 = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(db, new AcceptReceiptCommand(tenantAId, entityA1Id, "IDR", grn2.Receipt.GoodsReceiptId, "idem-s08-seed2-acc", new DateOnly(2026, 11, 3), StockStatuses.QualityInspection, "Seed QI",
            [new AcceptReceiptLineInput(grLine2.GoodsReceiptLineId, 10m)], clerkId, now), CancellationToken.None);
        Assert.True(acc2.Succeeded, $"{acc2.ErrorCode}: {acc2.ErrorDetail}");

        var locMain = layout.ResolveForStatus(StockStatuses.Unrestricted);
        var locQc = layout.ResolveForStatus(StockStatuses.QualityInspection);
        var locBlk = layout.ResolveForStatus(StockStatuses.Blocked);

        var seedInspection = acc2.CreatedInspections![0];
        var dispSeed = await QualityInspectionService.RecordDispositionAsync(db, new RecordInspectionDispositionCommand(
            tenantAId, entityA1Id, seedInspection.ReceiptInspectionId, new DateOnly(2026, 11, 4),
            2m, locMain.LocationId, 0m, null, QualityReasonCodes.PassedSpecification, "QC-SEED", "Sample pass", "idem-s08-seed-disp",
            inspectorId, now), CancellationToken.None);
        Assert.True(dispSeed.Succeeded, $"{dispSeed.ErrorCode}: {dispSeed.ErrorDetail}");

        var unrBalId = await db.StockLocationBalances
            .Where(b => b.TenantId == tenantAId && b.LegalEntityId == entityA1Id && b.ItemId == itemUnr.ItemId && b.LocationId == locMain.LocationId)
            .Select(b => b.StockLocationBalanceId)
            .SingleAsync();

        await seedTx.CommitAsync();

        return new S08SeedWorld(
            tenantAId,
            entityA1Id,
            entityA2Id,
            tenantBId,
            entityB1Id,
            clerkId,
            clerkEmail,
            approverId,
            approverEmail,
            inspectorId,
            inspectorEmail,
            foreignId,
            foreignEmail,
            supA.PartnerId,
            pcsA.UomId,
            itemUnr.ItemId,
            itemQi.ItemId,
            seedInspection.ReceiptInspectionId,
            dispSeed.Disposition!.InspectionDispositionId,
            unrBalId,
            layout.DefaultWarehouse.WarehouseId,
            locMain.LocationId,
            locQc.LocationId,
            locBlk.LocationId,
            pw);
    }
}
