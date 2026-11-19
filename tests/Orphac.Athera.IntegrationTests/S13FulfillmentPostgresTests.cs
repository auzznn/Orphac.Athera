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
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S13FulfillmentPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S13SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid WarehouseMgrA1Id,
        string WarehouseMgrA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid ReaderA2Id,
        string ReaderA2Email,
        Guid ForeignUserB1Id,
        string ForeignUserB1Email,
        Guid CustomerAId,
        Guid PcsUomAId,
        Guid ItemValveAId,
        Guid ItemLotChemAId,
        Guid WarehouseA1Id,
        Guid LocMainA1Id,
        Guid LocQcA1Id,
        Guid LocBlkA1Id,
        Guid ValidLotA1Id,
        Guid ExpiredLotA1Id,
        Guid PpnOutTaxA1Id,
        Guid ApprovedSo1Id,
        Guid ApprovedSo1LineValveId,
        Guid ApprovedSo2Id,
        Guid ApprovedSo2LineValveId,
        Guid ApprovedSoLotId,
        Guid ApprovedSoLotLineId,
        Guid DraftSoId,
        Guid DraftSoLineId,
        string Password);

    [Fact]
    public async Task S13_PG_01_ForcedRlsCrossTenantIsolationAndDispatchedShipmentTriggerImmutability()
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
                WHERE n.nspname = 'fulfillment'
                  AND c.relname IN ('pick_sequences', 'shipment_sequences', 'pick_orders', 'pick_lines', 'shipments', 'shipment_lines')
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(6L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Seed a dispatched shipment on Tenant A via coordinator
        Guid shipmentId;
        Guid shipmentLineId;
        await using (var mDb = fx.CreateMigratorDbContext())
        {
            var rsv = await SalesFulfillmentCoordinator.ReserveSalesOrderLineAsync(
                mDb,
                new ReserveSalesOrderLineCommand(
                    world.TenantAId, world.EntityA1Id, world.ApprovedSo1Id, world.ApprovedSo1LineValveId,
                    world.LocMainA1Id, null, 4m, "pg01-rsv-1", null, world.WarehouseMgrA1Id, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(rsv.Succeeded);

            var pick = await SalesFulfillmentCoordinator.CreatePickOrderAsync(
                mDb,
                new CreatePickOrderCommand(
                    world.TenantAId, world.EntityA1Id, world.ApprovedSo1Id, true, "pg01-pck-1", null,
                    [new PickLineInput(rsv.Reservation!.StockReservationId, 4m, 4m)],
                    world.WarehouseMgrA1Id, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(pick.Succeeded);

            var shp = await SalesFulfillmentCoordinator.DispatchShipmentAsync(
                mDb,
                new DispatchSalesShipmentCommand(
                    world.TenantAId, world.EntityA1Id, "IDR", pick.PickOrder!.PickOrderId,
                    new DateOnly(2026, 11, 19), "SJ-PG01-001", "JNE", "B 1234 XY", "pg01-shp-1", null,
                    null, world.WarehouseMgrA1Id, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(shp.Succeeded);
            shipmentId = shp.Shipment!.ShipmentId;
            shipmentLineId = shp.Lines[0].ShipmentLineId;
        }

        // 1. Missing tenant context on runtime role returns 0 rows
        await using (var noCtxTx = await conn.BeginTransactionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = noCtxTx;
            cmd.CommandText = "SELECT count(*) FROM fulfillment.shipments;";
            Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
            await noCtxTx.CommitAsync();
        }

        // 2. Foreign Tenant B context cannot read Tenant A shipments or pick orders
        await using (var tenantBTx = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, tenantBTx, world.TenantBId);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tenantBTx;
            cmd.CommandText = "SELECT count(*) FROM fulfillment.shipments;";
            Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
            await tenantBTx.CommitAsync();
        }

        // 3. Dispatched shipment immutability trigger blocks cost/quantity mutation & line mutation
        await using (var mutateCostTx = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, mutateCostTx, world.TenantAId);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = mutateCostTx;
            cmd.CommandText = "UPDATE fulfillment.shipments SET total_cogs_amount = 1 WHERE shipment_id = @sid;";
            cmd.Parameters.AddWithValue("sid", shipmentId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("shipment_dispatched_immutable", ex.MessageText, StringComparison.Ordinal);
            await mutateCostTx.RollbackAsync();
        }

        await using (var mutateLineTx = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, mutateLineTx, world.TenantAId);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = mutateLineTx;
            cmd.CommandText = "UPDATE fulfillment.shipment_lines SET dispatched_base_quantity = 1 WHERE shipment_line_id = @lid;";
            cmd.Parameters.AddWithValue("lid", shipmentLineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
            await mutateLineTx.RollbackAsync();
        }

        await using (var mConn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await mConn.OpenAsync();
            await using var cmd = mConn.CreateCommand();
            cmd.CommandText = "UPDATE fulfillment.shipment_lines SET dispatched_base_quantity = 1 WHERE shipment_line_id = @lid;";
            cmd.Parameters.AddWithValue("lid", shipmentLineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("shipment_dispatched_immutable", ex.MessageText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task S13_PG_02_CompetingSalesOrdersConcurrentReserveRaceNeverOversells()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        // LocMainA1 has 20 PCS UNRESTRICTED of ItemValveA.
        // ApprovedSo1 (15 PCS) and ApprovedSo2 (15 PCS) concurrently try to reserve 15 PCS each (15 + 15 = 30 > 20).
        async Task<SalesReservationOutcome> TryReserveAsync(Guid soId, Guid soLineId, string key)
        {
            var options = new DbContextOptionsBuilder<Orphac.Athera.Modules.Persistence.AtheraDbContext>()
                .UseAtheraNpgsql(fx.RuntimeConnectionString)
                .Options;
            await using var db = new Orphac.Athera.Modules.Persistence.AtheraDbContext(options);
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {world.TenantAId.ToString("D")}, true), set_config('app.actor_user_id', {world.WarehouseMgrA1Id.ToString("D")}, true);");

            var outcome = await SalesFulfillmentCoordinator.ReserveSalesOrderLineAsync(
                db,
                new ReserveSalesOrderLineCommand(
                    world.TenantAId, world.EntityA1Id, soId, soLineId,
                    world.LocMainA1Id, null, 15m, key, "Concurrent reserve race",
                    world.WarehouseMgrA1Id, DateTimeOffset.UtcNow),
                CancellationToken.None);

            if (outcome.Succeeded)
            {
                await tx.CommitAsync();
            }
            else
            {
                await tx.RollbackAsync();
            }

            return outcome;
        }

        var outcomes = await Task.WhenAll(
            TryReserveAsync(world.ApprovedSo1Id, world.ApprovedSo1LineValveId, "race-so1-15"),
            TryReserveAsync(world.ApprovedSo2Id, world.ApprovedSo2LineValveId, "race-so2-15"));

        Assert.Equal(1, outcomes.Count(o => o.Succeeded));
        var failed = Assert.Single(outcomes, o => !o.Succeeded);
        Assert.Equal(409, failed.HttpStatus);
        Assert.Equal("od10_insufficient_available_stock", failed.ErrorCode);

        await using var verifyDb = fx.CreateMigratorDbContext();
        var bal = await verifyDb.StockLocationBalances.AsNoTracking()
            .SingleAsync(b => b.TenantId == world.TenantAId && b.LegalEntityId == world.EntityA1Id && b.LocationId == world.LocMainA1Id && b.ItemId == world.ItemValveAId);
        Assert.Equal(20m, bal.OnHandBaseQuantity);
        Assert.Equal(15m, bal.ReservedBaseQuantity);
        Assert.Equal(5m, bal.AvailableBaseQuantity);
    }

    [Fact]
    public async Task S13_API_01_BlocksQuarantinedBlockedStockRequiredOrExpiredLotAndUnapprovedSalesOrder()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var client = await SignInAndGetCsrfAsync(factory, world.WarehouseMgrA1Email, world.Password);

        // 1. Unapproved DRAFT sales order cannot reserve stock
        var unapprovedRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.DraftSoId, world.DraftSoLineId, world.LocMainA1Id, null, 2m, "api01-unapp"));
        Assert.Equal(HttpStatusCode.Conflict, unapprovedRes.StatusCode);
        Assert.Contains("so_not_approved_for_fulfillment", await unapprovedRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // 2. Quarantined (LOC-QC) and Blocked (LOC-BLK) locations are rejected
        var qcRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSo1Id, world.ApprovedSo1LineValveId, world.LocQcA1Id, null, 2m, "api01-qc"));
        Assert.Equal(HttpStatusCode.Conflict, qcRes.StatusCode);
        Assert.Contains("od10_restricted_stock_not_reservable", await qcRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var blkRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSo1Id, world.ApprovedSo1LineValveId, world.LocBlkA1Id, null, 2m, "api01-blk"));
        Assert.Equal(HttpStatusCode.Conflict, blkRes.StatusCode);
        Assert.Contains("od10_restricted_stock_not_reservable", await blkRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // 3. Lot-required item without lot or with expired lot is rejected
        var missingLotRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSoLotId, world.ApprovedSoLotLineId, world.LocMainA1Id, null, 2m, "api01-nolot"));
        Assert.Equal(HttpStatusCode.Conflict, missingLotRes.StatusCode);
        Assert.Contains("lot_required_by_item_policy", await missingLotRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var expiredLotRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSoLotId, world.ApprovedSoLotLineId, world.LocMainA1Id, world.ExpiredLotA1Id, 2m, "api01-explot"));
        Assert.Equal(HttpStatusCode.Conflict, expiredLotRes.StatusCode);
        Assert.Contains("lot_expired_for_fulfillment", await expiredLotRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // 4. Valid active lot at LOC-MAIN succeeds
        var validLotRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSoLotId, world.ApprovedSoLotLineId, world.LocMainA1Id, world.ValidLotA1Id, 3m, "api01-validlot"));
        Assert.Equal(HttpStatusCode.Created, validLotRes.StatusCode);
        var validRsvDto = await validLotRes.Content.ReadFromJsonAsync<SalesReservationDto>(JsonOptions);
        Assert.NotNull(validRsvDto);
        Assert.Equal(3m, validRsvDto!.ReservedBaseQuantity);
        Assert.Equal("LOT-CHEM-VALID-01", validRsvDto.LotNumber);
    }

    [Fact]
    public async Task S13_API_02_EndToEndReservePickPartialDispatchAtomicWeightedAverageCogsDeliveryConfirmAndIdempotency()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var client = await SignInAndGetCsrfAsync(factory, world.WarehouseMgrA1Email, world.Password);

        // 1. Reserve 6 PCS of ApprovedSo1 (15 PCS ordered, 20 PCS UNRESTRICTED @ Rp 40.000/PCS in pool)
        var rsvRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSo1Id, world.ApprovedSo1LineValveId, world.LocMainA1Id, null, 6m, "api02-rsv-6", "Reserve 6 PCS"));
        Assert.Equal(HttpStatusCode.Created, rsvRes.StatusCode);
        var rsvDto = (await rsvRes.Content.ReadFromJsonAsync<SalesReservationDto>(JsonOptions))!;
        Assert.Equal(6m, rsvDto.ReservedBaseQuantity);
        Assert.Equal(ReservationStatuses.Active, rsvDto.Status);

        // 2. Create Pick Order (OPEN) for 6 PCS, then confirm picked 6 PCS
        var pickCreateRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/picks",
            new CreatePickOrderRequest(
                world.ApprovedSo1Id,
                ConfirmImmediately: false,
                "api02-pck-1",
                "Pick 6 PCS valve",
                [new PickLineInputDto(rsvDto.StockReservationId, 6m)]));
        Assert.Equal(HttpStatusCode.Created, pickCreateRes.StatusCode);
        var openPickDto = (await pickCreateRes.Content.ReadFromJsonAsync<PickOrderDto>(JsonOptions))!;
        Assert.Equal(PickOrderStatuses.Open, openPickDto.Status);

        // Attempting to dispatch before pick confirmation fails (409 pick_not_ready_for_dispatch)
        var prematureDispatchRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/shipments",
            new DispatchSalesShipmentRequest(
                openPickDto.PickOrderId, new DateOnly(2026, 11, 19), "SJ-PREMATURE", "JNE", null, "api02-shp-premature", null));
        Assert.Equal(HttpStatusCode.Conflict, prematureDispatchRes.StatusCode);
        Assert.Contains("pick_not_ready_for_dispatch", await prematureDispatchRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Confirm pick order
        var pickConfirmRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/picks/{openPickDto.PickOrderId}/confirm",
            new ConfirmPickOrderRequest([new ConfirmPickLineInputDto(openPickDto.Lines[0].PickLineId, 6m)]));
        Assert.Equal(HttpStatusCode.OK, pickConfirmRes.StatusCode);
        var confirmedPickDto = (await pickConfirmRes.Content.ReadFromJsonAsync<PickOrderDto>(JsonOptions))!;
        Assert.Equal(PickOrderStatuses.Picked, confirmedPickDto.Status);
        Assert.Equal(6m, confirmedPickDto.TotalPickedBaseQuantity);

        // 3. Partial dispatch 4 PCS out of 6 PCS picked (moving-average cost = Rp 40.000/PCS -> COGS = Rp 160.000)
        var dispatchReq = new DispatchSalesShipmentRequest(
            confirmedPickDto.PickOrderId,
            new DateOnly(2026, 11, 19),
            "SJ-JKT-2026-901",
            "JNE Trucking",
            "B 9481 KJD",
            "api02-shp-partial-4",
            "Partial dispatch 4 PCS",
            [new DispatchShipmentLineInputDto(confirmedPickDto.Lines[0].PickLineId, 4m)]);

        var dispatchRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/shipments",
            dispatchReq);
        Assert.Equal(HttpStatusCode.Created, dispatchRes.StatusCode);
        var shpDto = (await dispatchRes.Content.ReadFromJsonAsync<SalesShipmentDto>(JsonOptions))!;
        Assert.Equal(ShipmentStatuses.Dispatched, shpDto.Status);
        Assert.Equal(4m, shpDto.TotalDispatchedBaseQuantity);
        Assert.Equal(160_000m, shpDto.TotalCogsAmount);
        Assert.StartsWith("MOV-2026-", shpDto.StockMovementNumber, StringComparison.Ordinal);
        Assert.StartsWith("JRN-2026-", shpDto.GlJournalNumber, StringComparison.Ordinal);

        // 4. Retry dispatch with identical IdempotencyKey -> 200 OK replay without duplicate stock/GL entries
        var replayRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/shipments",
            dispatchReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayDto = (await replayRes.Content.ReadFromJsonAsync<SalesShipmentDto>(JsonOptions))!;
        Assert.Equal(shpDto.ShipmentId, replayDto.ShipmentId);
        Assert.Equal(shpDto.GlJournalId, replayDto.GlJournalId);
        Assert.Equal(shpDto.StockMovementId, replayDto.StockMovementId);

        // 5. Verify DB conservation: OnHand = 16 (20 - 4), Reserved = 2 (6 - 4), Available = 14 (20 - 6), Pool = 16 @ 40.000 = 640.000, Journal balanced 160.000
        await using (var verifyDb = fx.CreateMigratorDbContext())
        {
            var pos = await verifyDb.StockPositions.AsNoTracking()
                .SingleAsync(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.ItemId == world.ItemValveAId);
            Assert.Equal(16m, pos.UnrestrictedBaseQuantity);
            Assert.Equal(2m, pos.ReservedBaseQuantity);
            Assert.Equal(14m, pos.AvailableBaseQuantity);

            var pool = await verifyDb.ValuationPools.AsNoTracking()
                .SingleAsync(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.ItemId == world.ItemValveAId);
            Assert.Equal(26m, pool.ValuedBaseQuantity);
            Assert.Equal(1_040_000m, pool.CarryingValueAmount);
            Assert.Equal(40_000m, pool.MovingAverageUnitCost);

            var jLines = await verifyDb.JournalLines.AsNoTracking()
                .Where(l => l.TenantId == world.TenantAId && l.LegalEntityId == world.EntityA1Id && l.JournalId == shpDto.GlJournalId)
                .ToListAsync();
            Assert.Equal(2, jLines.Count);
            Assert.Equal(160_000m, jLines.Sum(l => l.DebitAmount));
            Assert.Equal(160_000m, jLines.Sum(l => l.CreditAmount));

            var so = await verifyDb.SalesOrders.AsNoTracking()
                .SingleAsync(o => o.TenantId == world.TenantAId && o.LegalEntityId == world.EntityA1Id && o.SalesOrderId == world.ApprovedSo1Id);
            Assert.Equal(2m, so.TotalReservedBaseQuantity);
            Assert.Equal(4m, so.TotalDispatchedBaseQuantity);
        }

        // 6. Confirm delivery evidence (DISPATCHED -> DELIVERED)
        var deliverRes = await client.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/shipments/{shpDto.ShipmentId}/confirm-delivery",
            new ConfirmShipmentDeliveryRequest("Tari Wibowo", "POD-GRN-2026-901"));
        Assert.Equal(HttpStatusCode.OK, deliverRes.StatusCode);
        var deliveredDto = (await deliverRes.Content.ReadFromJsonAsync<SalesShipmentDto>(JsonOptions))!;
        Assert.Equal(ShipmentStatuses.Delivered, deliveredDto.Status);
        Assert.Equal("Tari Wibowo", deliveredDto.DeliveryConfirmedByName);
        Assert.Equal("POD-GRN-2026-901", deliveredDto.DeliveryEvidenceReference);
    }

    [Fact]
    public async Task S13_API_03_OrderCancellationBlockedWhileReservedReleaseUnblocksAndHostileScopeDenied()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var whClient = await SignInAndGetCsrfAsync(factory, world.WarehouseMgrA1Email, world.Password);

        // Reserve 5 PCS on ApprovedSo1
        var rsvRes = await whClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSo1Id, world.ApprovedSo1LineValveId, world.LocMainA1Id, null, 5m, "api03-rsv-5"));
        Assert.Equal(HttpStatusCode.Created, rsvRes.StatusCode);
        var rsvDto = (await rsvRes.Content.ReadFromJsonAsync<SalesReservationDto>(JsonOptions))!;

        // Cancelling ApprovedSo1 while 5 PCS are reserved is blocked by OD-09 (409 so_has_downstream_fulfillment)
        var cancelBlockedRes = await whClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/sales-orders/{world.ApprovedSo1Id}/cancel",
            new CloseOrCancelSoRequest("Customer requested cancellation"));
        Assert.Equal(HttpStatusCode.Conflict, cancelBlockedRes.StatusCode);
        Assert.Contains("so_has_downstream_fulfillment", await cancelBlockedRes.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Release the 5 PCS reservation -> unblocks order cancellation
        var releaseRes = await whClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/reservations/{rsvDto.StockReservationId}/release",
            new ReleaseSalesReservationRequest(null));
        Assert.Equal(HttpStatusCode.OK, releaseRes.StatusCode);
        var releasedDto = (await releaseRes.Content.ReadFromJsonAsync<SalesReservationDto>(JsonOptions))!;
        Assert.Equal(ReservationStatuses.Released, releasedDto.Status);
        Assert.Equal(0m, releasedDto.RemainingReservedBaseQuantity);

        var cancelOkRes = await whClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/sales-orders/{world.ApprovedSo1Id}/cancel",
            new CloseOrCancelSoRequest("Customer requested cancellation after reservation release"));
        Assert.Equal(HttpStatusCode.OK, cancelOkRes.StatusCode);

        // Read-only viewer on EntityA2 can GET overview (200) but cannot POST reservation (403)
        using var readerClient = await SignInAndGetCsrfAsync(factory, world.ReaderA2Email, world.Password);
        var overviewA2 = await readerClient.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/fulfillment/overview");
        Assert.Equal(HttpStatusCode.OK, overviewA2.StatusCode);

        var forbiddenMutate = await readerClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(world.ApprovedSo2Id, world.ApprovedSo2LineValveId, world.LocMainA1Id, null, 1m, "api03-ro-deny"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMutate.StatusCode);

        // Hostile foreign tenant B user targeting Tenant A receives 404 NotFound
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignUserB1Email, world.Password);
        var hostileGet = await foreignClient.GetAsync($"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/fulfillment/overview");
        Assert.Equal(HttpStatusCode.NotFound, hostileGet.StatusCode);
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tenantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
        cmd.Parameters.AddWithValue("tid", tenantId.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
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

    private static async Task<S13SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (whMgrId, whMgrEmail, _) = await fx.CreateTestUserAsync("wh.a1.s13@test.local", "Rina Warehouse", pw);
        var (approverId, approverEmail, _) = await fx.CreateTestUserAsync("approver.a1.s13@test.local", "Budi Approver", pw);
        var (readerId, readerEmail, _) = await fx.CreateTestUserAsync("reader.a2.s13@test.local", "Andi Reader", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.b1.s13@test.local", "Samudra Staff", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-45);
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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, whMgrId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approverId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, whMgrId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, whMgrId, PermissionCodes.InventoryRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, whMgrId, PermissionCodes.InventoryManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, whMgrId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, whMgrId, PermissionCodes.SalesManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.SalesApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerId, PermissionCodes.InventoryRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerId, PermissionCodes.SalesRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, PermissionCodes.InventoryManage, now, null, now));

        db.ApprovalPolicies.Add(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now));

        // Seed GL CoA (1300 Inventory Asset, 5100 COGS) & open 2026-11 period
        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, false, now);
        var cogsAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "Harga Pokok Penjualan (COGS)", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, false, now);
        db.GlAccounts.AddRange(invAcc, cogsAcc);
        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcc, "Inventory Asset 1300", whMgrId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcc, "COGS 5100", whMgrId, now));
        db.FinancialPeriods.Add(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        var cust = Partner.Create(Guid.NewGuid(), tenantAId, "PT-CUST-001", "PT Graha Ritel Nusantara", "Graha Ritel", false, true, "01.777.332.1-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        db.Partners.Add(cust);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);

        var itemValve = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S09-VALVE-01", "Katup Industri PN16", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        var itemLotChem = Item.Create(Guid.NewGuid(), tenantAId, "ITM-CHEM-LOT-01", "Pelarut Industri Grade A", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now, LotTrackingPolicies.Required);
        db.Items.AddRange(itemValve, itemLotChem);

        var ppnOut = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPN-OUT-11", "PPN Keluaran 11%", TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, null, new DateOnly(2025, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, "OD-07 Output", now);
        db.TaxCodes.Add(ppnOut);
        await db.SaveChangesAsync();

        var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);
        var locMain = layout.LocationsByCode[InventoryAvailabilityDefaults.DefaultMainLocationCode];
        var locQc = layout.LocationsByCode[InventoryAvailabilityDefaults.DefaultQuarantineLocationCode];
        var locBlk = layout.LocationsByCode[InventoryAvailabilityDefaults.DefaultBlockedLocationCode];

        // Seed 20 PCS UNRESTRICTED + 5 PCS QUALITY_INSPECTION + 5 PCS BLOCKED for itemValve (@ Rp 40.000/PCS)
        var posValve = StockPosition.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, pcs.UomId, now);
        posValve.ApplyInboundReceipt(20m, StockStatuses.Unrestricted, now);
        posValve.ApplyInboundReceipt(5m, StockStatuses.QualityInspection, now);
        posValve.ApplyInboundReceipt(5m, StockStatuses.Blocked, now);

        var poolValve = ValuationPool.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, "IDR", pcs.UomId, now);
        poolValve.ApplyReceiptValuation(30m, 1_200_000m, now); // 30 PCS @ Rp 40.000

        var balValveMain = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, posValve.StockPositionId, itemValve.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locMain.LocationId, null, StockStatuses.Unrestricted, now);
        balValveMain.AdjustOnHand(20m, now);

        var balValveQc = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, posValve.StockPositionId, itemValve.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locQc.LocationId, null, StockStatuses.QualityInspection, now);
        balValveQc.AdjustOnHand(5m, now);

        var balValveBlk = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, posValve.StockPositionId, itemValve.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locBlk.LocationId, null, StockStatuses.Blocked, now);
        balValveBlk.AdjustOnHand(5m, now);

        // Seed Lot-tracked item (10 PCS valid lot + 5 PCS expired lot)
        var validLot = StockLot.Create(Guid.NewGuid(), tenantAId, entityA1Id, itemLotChem.ItemId, "LOT-CHEM-VALID-01", "SUP-LOT-01", new DateOnly(2026, 6, 1), new DateOnly(2027, 6, 1), null, whMgrId, now);
        var expiredLot = StockLot.Create(Guid.NewGuid(), tenantAId, entityA1Id, itemLotChem.ItemId, "LOT-CHEM-EXPIRED-99", "SUP-LOT-99", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), null, whMgrId, now);
        db.StockLots.AddRange(validLot, expiredLot);

        var posChem = StockPosition.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemLotChem.ItemId, pcs.UomId, now);
        posChem.ApplyInboundReceipt(15m, StockStatuses.Unrestricted, now);
        var poolChem = ValuationPool.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemLotChem.ItemId, "IDR", pcs.UomId, now);
        poolChem.ApplyReceiptValuation(15m, 750_000m, now); // Rp 50.000/PCS

        var balChemValid = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, posChem.StockPositionId, itemLotChem.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locMain.LocationId, validLot.StockLotId, StockStatuses.Unrestricted, now);
        balChemValid.AdjustOnHand(10m, now);

        var balChemExpired = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, posChem.StockPositionId, itemLotChem.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locMain.LocationId, expiredLot.StockLotId, StockStatuses.Unrestricted, now);
        balChemExpired.AdjustOnHand(5m, now);

        db.StockPositions.AddRange(posValve, posChem);
        db.ValuationPools.AddRange(poolValve, poolChem);
        db.StockLocationBalances.AddRange(balValveMain, balValveQc, balValveBlk, balChemValid, balChemExpired);
        await db.SaveChangesAsync();

        // Create & approve SO-2026-00001 (15 PCS Valve), SO-2026-00002 (15 PCS Valve), SO-2026-00003 (5 PCS LotChem), plus unapproved DraftSo
        async Task<(Guid SoId, Guid LineId)> CreateSalesOrderHelperAsync(Guid itemId, decimal qty, bool approve)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var draft = await SalesOrderCoordinator.CreateDraftAsync(
                db,
                new CreateSoDraftCommand(
                    tenantAId, entityA1Id, "IDR", "IDR", cust.PartnerId,
                    new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 25),
                    "Gudang Bekasi Timur", 14, "PO-CUST-S13", false, null,
                    [new SoLineInput("SO-L001", itemId, pcs.UomId, qty, 95_000m, 0m, ppnOut.TaxCodeId, null, null)],
                    whMgrId, now),
                CancellationToken.None);
            Assert.True(draft.Succeeded);

            if (approve)
            {
                var sub = await SalesOrderCoordinator.SubmitRevisionAsync(
                    db,
                    new SubmitSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder!.SalesOrderId, draft.Revision!.RevisionId, whMgrId, now.AddMinutes(1)),
                    CancellationToken.None);
                Assert.True(sub.Succeeded);

                var dec = await SalesOrderCoordinator.DecideRevisionAsync(
                    db,
                    new DecideSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder.SalesOrderId, draft.Revision.RevisionId, ApprovalDecisions.Approved, "Approved for S13 test", null, approverId, now.AddMinutes(2)),
                    CancellationToken.None);
                Assert.True(dec.Succeeded);
            }

            await tx.CommitAsync();
            var lineId = await db.SalesOrderLines.AsNoTracking()
                .Where(l => l.TenantId == tenantAId && l.LegalEntityId == entityA1Id && l.RevisionId == draft.Revision!.RevisionId)
                .Select(l => l.SoLineId)
                .SingleAsync();
            return (draft.SalesOrder!.SalesOrderId, lineId);
        }

        var (so1Id, so1LineId) = await CreateSalesOrderHelperAsync(itemValve.ItemId, 15m, approve: true);
        var (so2Id, so2LineId) = await CreateSalesOrderHelperAsync(itemValve.ItemId, 15m, approve: true);
        var (soLotId, soLotLineId) = await CreateSalesOrderHelperAsync(itemLotChem.ItemId, 5m, approve: true);
        var (draftSoId, draftSoLineId) = await CreateSalesOrderHelperAsync(itemValve.ItemId, 5m, approve: false);

        return new S13SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            whMgrId, whMgrEmail, approverId, approverEmail, readerId, readerEmail, foreignId, foreignEmail,
            cust.PartnerId, pcs.UomId, itemValve.ItemId, itemLotChem.ItemId,
            layout.DefaultWarehouse.WarehouseId, locMain.LocationId, locQc.LocationId, locBlk.LocationId,
            validLot.StockLotId, expiredLot.StockLotId, ppnOut.TaxCodeId,
            so1Id, so1LineId, so2Id, so2LineId, soLotId, soLotLineId, draftSoId, draftSoLineId, pw);
    }
}
