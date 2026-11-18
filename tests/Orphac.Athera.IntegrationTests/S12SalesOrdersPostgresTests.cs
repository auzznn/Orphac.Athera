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
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S12SalesOrdersPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S12SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid SalesOfficerA1Id,
        string SalesOfficerA1Email,
        Guid ApproverA1Id,
        string ApproverA1Email,
        Guid DualRoleA1Id,
        string DualRoleA1Email,
        Guid ReaderA2Id,
        string ReaderA2Email,
        Guid MemberB1NoSalesId,
        string MemberB1NoSalesEmail,
        Guid ForeignMgrB1Id,
        string ForeignMgrB1Email,
        Guid CustomerAllAId,
        Guid SupplierOnlyAId,
        Guid ForeignCustomerBId,
        Guid PcsUomAId,
        Guid BoxUomAId,
        Guid ItemStockAId,
        Guid ItemPurchaseOnlyAId,
        Guid PpnOutTaxA1Id,
        Guid PpnInTaxA1Id,
        string Password);

    [Fact]
    public async Task S12_PG_01_ForcedRlsMissingContextAndCrossTenantIsolationOnSalesTables()
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
                WHERE n.nspname = 'sales'
                  AND c.relname IN ('sales_order_sequences', 'sales_orders', 'sales_order_revisions', 'sales_order_lines')
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(4L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Seed Tenant B sequence row via migrator to test cross-tenant SELECT/WRITE denial
        await using (var mConn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await mConn.OpenAsync();
            await using var insSeq = mConn.CreateCommand();
            insSeq.CommandText = """
                INSERT INTO sales.sales_order_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2026, 5);
                """;
            insSeq.Parameters.AddWithValue("tid", world.TenantBId);
            insSeq.Parameters.AddWithValue("eid", world.EntityB1Id);
            await insSeq.ExecuteNonQueryAsync();
        }

        await using (var zeroRead = conn.CreateCommand())
        {
            zeroRead.CommandText = "SELECT count(*) FROM sales.sales_order_sequences;";
            Assert.Equal(0L, Convert.ToInt64(await zeroRead.ExecuteScalarAsync()));
        }

        await using (var writeNoCtx = conn.CreateCommand())
        {
            writeNoCtx.CommandText = """
                INSERT INTO sales.sales_order_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2027, 1);
                """;
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
            readForeign.CommandText = "SELECT count(*) FROM sales.sales_order_sequences WHERE tenant_id = @tb;";
            readForeign.Parameters.AddWithValue("tb", world.TenantBId);
            Assert.Equal(0L, Convert.ToInt64(await readForeign.ExecuteScalarAsync()));
        }

        await using (var writeForeign = conn.CreateCommand())
        {
            writeForeign.Transaction = tx;
            writeForeign.CommandText = """
                INSERT INTO sales.sales_order_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tb, @eb, 2028, 1);
                """;
            writeForeign.Parameters.AddWithValue("tb", world.TenantBId);
            writeForeign.Parameters.AddWithValue("eb", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeForeign.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task S12_PG_02_MigrationM12DownAndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261118183609_M11_SupplierSettlementOutboundClearingAndBankReconciliation");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'sales';";
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCheck = conn.CreateCommand();
            upCheck.CommandText = """
                SELECT count(*) FROM information_schema.tables
                WHERE table_schema = 'sales'
                  AND table_name IN ('sales_order_sequences', 'sales_orders', 'sales_order_revisions', 'sales_order_lines');
                """;
            Assert.Equal(4L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S12_PG_03_RuntimeDeleteDeniedSqlFreezeTriggersOd09CheckAndFourEyesConstraintEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var seller = await CreateSignedInClientAsync(factory, world.SalesOfficerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
        var createRes = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 22),
            "Gudang Pelanggan Bekasi", 14, "PO-CUST-001", false, "Freeze check",
            [new SoLineInputDto(null, world.ItemStockAId, world.BoxUomAId, 2m, 240_000m, 0m, world.PpnOutTaxA1Id)]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var revId = created.Revisions[0].RevisionId;
        var lineId = created.Revisions[0].Lines[0].SoLineId;

        var submitRes = await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{created.SalesOrderId:D}/revisions/{revId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        var approveRes = await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{created.SalesOrderId:D}/revisions/{revId:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Approved", null));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        // DELETE denied on sales_orders & sales_order_revisions
        await using (var sp0 = conn.CreateCommand()) { sp0.Transaction = tx; sp0.CommandText = "SAVEPOINT sp_del_so;"; await sp0.ExecuteNonQueryAsync(); }
        await using (var delSo = conn.CreateCommand())
        {
            delSo.Transaction = tx;
            delSo.CommandText = "DELETE FROM sales.sales_orders WHERE sales_order_id = @id;";
            delSo.Parameters.AddWithValue("id", created.SalesOrderId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delSo.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await using (var rb0 = conn.CreateCommand()) { rb0.Transaction = tx; rb0.CommandText = "ROLLBACK TO SAVEPOINT sp_del_so;"; await rb0.ExecuteNonQueryAsync(); }

        // Freeze trigger blocks commercial column mutation on frozen line (SQLSTATE 55000)
        await using (var sp1 = conn.CreateCommand()) { sp1.Transaction = tx; sp1.CommandText = "SAVEPOINT sp_line_mut;"; await sp1.ExecuteNonQueryAsync(); }
        await using (var mutLine = conn.CreateCommand())
        {
            mutLine.Transaction = tx;
            mutLine.CommandText = "UPDATE sales.sales_order_lines SET unit_price = 999 WHERE so_line_id = @lid;";
            mutLine.Parameters.AddWithValue("lid", lineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => mutLine.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
            Assert.Contains("so_revision_frozen_immutable", ex.Message);
        }
        await using (var rb1 = conn.CreateCommand()) { rb1.Transaction = tx; rb1.CommandText = "ROLLBACK TO SAVEPOINT sp_line_mut;"; await rb1.ExecuteNonQueryAsync(); }

        // Freeze trigger blocks deleting frozen line (SQLSTATE 55000)
        await using (var sp2 = conn.CreateCommand()) { sp2.Transaction = tx; sp2.CommandText = "SAVEPOINT sp_line_del;"; await sp2.ExecuteNonQueryAsync(); }
        await using (var delLine = conn.CreateCommand())
        {
            delLine.Transaction = tx;
            delLine.CommandText = "DELETE FROM sales.sales_order_lines WHERE so_line_id = @lid;";
            delLine.Parameters.AddWithValue("lid", lineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delLine.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }
        await using (var rb2 = conn.CreateCommand()) { rb2.Transaction = tx; rb2.CommandText = "ROLLBACK TO SAVEPOINT sp_line_del;"; await rb2.ExecuteNonQueryAsync(); }

        // Freeze trigger blocks mutating snapshot on revision (SQLSTATE 55000)
        await using (var sp3 = conn.CreateCommand()) { sp3.Transaction = tx; sp3.CommandText = "SAVEPOINT sp_rev_mut;"; await sp3.ExecuteNonQueryAsync(); }
        await using (var mutRev = conn.CreateCommand())
        {
            mutRev.Transaction = tx;
            mutRev.CommandText = "UPDATE sales.sales_order_revisions SET grand_total_amount = 1 WHERE revision_id = @rid;";
            mutRev.Parameters.AddWithValue("rid", revId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => mutRev.ExecuteNonQueryAsync());
            Assert.Equal("55000", ex.SqlState);
        }
        await using (var rb3 = conn.CreateCommand()) { rb3.Transaction = tx; rb3.CommandText = "ROLLBACK TO SAVEPOINT sp_rev_mut;"; await rb3.ExecuteNonQueryAsync(); }

        // Direct SQL check constraints: four-eyes (decided_by_user_id <> submitted_by_user_id) & discount_amount = 0
        await using (var sp4 = conn.CreateCommand()) { sp4.Transaction = tx; sp4.CommandText = "SAVEPOINT sp_ck_sod;"; await sp4.ExecuteNonQueryAsync(); }
        await using (var badSod = conn.CreateCommand())
        {
            badSod.Transaction = tx;
            badSod.CommandText = """
                INSERT INTO sales.sales_order_revisions (
                    revision_id, tenant_id, legal_entity_id, sales_order_id, revision_number, status, is_frozen,
                    customer_partner_id, customer_code_snapshot, customer_legal_name_snapshot, customer_pkp_status_snapshot,
                    currency_code, order_date, delivery_address_snapshot, payment_terms_days,
                    subtotal_amount, output_tax_total_amount, grand_total_amount, total_ordered_base_quantity, snapshot_hash,
                    created_by_user_id, created_at_utc, submitted_by_user_id, decided_by_user_id, updated_at_utc, version)
                VALUES (@rid, @tid, @eid, @soid, 99, 'APPROVED', false, @cid, 'CUST-A', 'Cust', 'PKP',
                        'IDR', '2026-11-15', 'Addr', 14, 100, 0, 100, 1, 'hash', @uid, now(), @uid, @uid, now(), 1);
                """;
            badSod.Parameters.AddWithValue("rid", Guid.NewGuid());
            badSod.Parameters.AddWithValue("tid", world.TenantAId);
            badSod.Parameters.AddWithValue("eid", world.EntityA1Id);
            badSod.Parameters.AddWithValue("soid", created.SalesOrderId);
            badSod.Parameters.AddWithValue("cid", world.CustomerAllAId);
            badSod.Parameters.AddWithValue("uid", world.SalesOfficerA1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => badSod.ExecuteNonQueryAsync());
            Assert.Equal("23514", ex.SqlState);
        }
        await using (var rb4 = conn.CreateCommand()) { rb4.Transaction = tx; rb4.CommandText = "ROLLBACK TO SAVEPOINT sp_ck_sod;"; await rb4.ExecuteNonQueryAsync(); }

        // Check OD-09 SQL constraint ck_so_lines_od09_zero_discount on an editable draft revision
        var draft2Res = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Gudang A", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 0m, null)]));
        var draft2 = (await draft2Res.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        await using (var sp5 = conn.CreateCommand()) { sp5.Transaction = tx; sp5.CommandText = "SAVEPOINT sp_ck_disc;"; await sp5.ExecuteNonQueryAsync(); }
        await using (var badDisc = conn.CreateCommand())
        {
            badDisc.Transaction = tx;
            badDisc.CommandText = """
                UPDATE sales.sales_order_lines SET discount_amount = 500 WHERE so_line_id = @lid;
                """;
            badDisc.Parameters.AddWithValue("lid", draft2.Revisions[0].Lines[0].SoLineId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => badDisc.ExecuteNonQueryAsync());
            Assert.Equal("23514", ex.SqlState);
        }

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task S12_API_01_DraftSubmitSelfApprovalBlockedIndependentApproveAndDuplicateNumberConflict()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var seller = await CreateSignedInClientAsync(factory, world.SalesOfficerA1Email, world.Password);
        using var dualActor = await CreateSignedInClientAsync(factory, world.DualRoleA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";

        // Reference data includes eligible customer & stock availability
        var refDto = await (await seller.GetAsync($"{baseA1}/sales-orders/reference-data")).Content.ReadFromJsonAsync<SalesOrdersReferenceDataResponse>(JsonOptions);
        Assert.NotNull(refDto);
        Assert.Contains(refDto.Customers, c => c.PartnerId == world.CustomerAllAId);
        var stockItemRef = refDto.Items.Single(i => i.ItemId == world.ItemStockAId);
        Assert.Equal(50m, stockItemRef.StockAvailability.AvailableBaseQuantity);

        // DualRole creates & submits SO1 -> SoNumber stays null while PENDING_APPROVAL
        var c1Res = await dualActor.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 20),
            "Gudang Jakarta Selatan", 14, "PO-SO-101", false, "Order 1",
            [new SoLineInputDto("SO-L001", world.ItemStockAId, world.BoxUomAId, 2m, 240_000m, 0m, world.PpnOutTaxA1Id)]));
        Assert.Equal(HttpStatusCode.Created, c1Res.StatusCode);
        var d1 = (await c1Res.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var r1Id = d1.Revisions[0].RevisionId;
        Assert.Null(d1.SoNumber);
        Assert.Equal(48m, d1.Revisions[0].TotalOrderedBaseQuantity); // 2 BOX * 24 PCS
        Assert.Equal(480_000m, d1.Revisions[0].SubtotalAmount);
        Assert.Equal(52_800m, d1.Revisions[0].OutputTaxTotalAmount); // 11% OUTPUT
        Assert.Equal(532_800m, d1.Revisions[0].GrandTotalAmount);

        var sub1Res = await dualActor.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r1Id:D}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, sub1Res.StatusCode);
        var sub1 = (await sub1Res.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        Assert.Null(sub1.SoNumber);
        Assert.Equal(SalesOrderStatuses.PendingApproval, sub1.Status);

        // Self-approval by DualRole blocked (403)
        var selfApproveRes = await dualActor.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r1Id:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Self approve attempt"));
        Assert.Equal(HttpStatusCode.Forbidden, selfApproveRes.StatusCode);

        // Independent approver approves with explicit SO-2026-00001
        var app1Res = await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r1Id:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Approved by controller", "SO-2026-00001"));
        Assert.Equal(HttpStatusCode.OK, app1Res.StatusCode);
        var app1 = (await app1Res.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        Assert.Equal("SO-2026-00001", app1.SoNumber);
        Assert.Equal(SalesOrderStatuses.Approved, app1.Status);

        // Hard delete blocked (409)
        var delRes = await seller.DeleteAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}");
        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);

        // Duplicate SO number blocked (409 duplicate_so_number)
        var c2 = (await (await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 16), null, "Gudang B", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 15_000m, 0m, null)]))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var r2Id = c2.Revisions[0].RevisionId;
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{c2.SalesOrderId:D}/revisions/{r2Id:D}/submit", new { })).StatusCode);
        var dupRes = await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{c2.SalesOrderId:D}/revisions/{r2Id:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Duplicate try", "SO-2026-00001"));
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);
    }

    [Fact]
    public async Task S12_API_02_ConcurrentApprovalsAllowExactlyOneCommit()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var seller = await CreateSignedInClientAsync(factory, world.SalesOfficerA1Email, world.Password);

        var approverClients = new List<HttpClient>();
        for (var i = 0; i < 6; i++)
        {
            approverClients.Add(await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password));
        }

        try
        {
            var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
            var created = (await (await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
                world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Gudang Concurrency", 14, null, false, null,
                [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 4m, 25_000m, 0m, world.PpnOutTaxA1Id)]))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
            var revId = created.Revisions[0].RevisionId;
            Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{created.SalesOrderId:D}/revisions/{revId:D}/submit", new { })).StatusCode);

            var tasks = approverClients.Select(c =>
                c.PostAsJsonAsync($"{baseA1}/sales-orders/{created.SalesOrderId:D}/revisions/{revId:D}/decide",
                    new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Concurrent approve"))).ToArray();
            var responses = await Task.WhenAll(tasks);

            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

            await using var verifyDb = fx.CreateMigratorDbContext();
            var outboxCount = await verifyDb.OutboxMessages.CountAsync(m =>
                m.TenantId == world.TenantAId && m.Topic == "sales.order.revision.approved.v1");
            Assert.Equal(1, outboxCount);
        }
        finally
        {
            foreach (var c in approverClients) c.Dispose();
        }
    }

    [Fact]
    public async Task S12_API_03_ApprovedRevisionSnapshotRemainsUnchangedAcrossAmendmentsAndDiffVerified()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var seller = await CreateSignedInClientAsync(factory, world.SalesOfficerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
        var d1 = (await (await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 20), "Gudang Lama", 14, "REF-1", false, null,
            [new SoLineInputDto("SO-L001", world.ItemStockAId, world.PcsUomAId, 10m, 20_000m, 0m, world.PpnOutTaxA1Id)]))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var r1Id = d1.Revisions[0].RevisionId;
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r1Id:D}/submit", new { })).StatusCode);
        var app1 = (await (await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r1Id:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Rev 1 baseline"))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var rev1Hash = app1.Revisions.Single(r => r.RevisionNumber == 1).SnapshotHash;

        // Amend Rev 2
        var amendRes = await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions", new CreateSoAmendmentRequest("Naik kuantitas 10 -> 15"));
        Assert.Equal(HttpStatusCode.Created, amendRes.StatusCode);
        var amended = (await amendRes.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var r2 = amended.Revisions.Single(r => r.RevisionNumber == 2);
        Assert.Equal(SalesOrderRevisionStatuses.Draft, r2.Status);
        Assert.Equal(SalesOrderStatuses.Approved, amended.Status);
        Assert.Equal(1, amended.CurrentApprovedRevisionNumber);

        // Update Rev 2 qty & address
        var updRes = await seller.PutAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r2.RevisionId:D}",
            new UpdateSalesOrderRevisionRequest(
                world.CustomerAllAId, new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 22), "Gudang Baru Bekasi", 30, "REF-1", false,
                "Naik kuantitas 10 -> 15", null,
                [new SoLineInputDto("SO-L001", world.ItemStockAId, world.PcsUomAId, 15m, 20_000m, 0m, world.PpnOutTaxA1Id)]));
        Assert.Equal(HttpStatusCode.OK, updRes.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r2.RevisionId:D}/submit", new { })).StatusCode);
        var app2Res = await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{d1.SalesOrderId:D}/revisions/{r2.RevisionId:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Rev 2 disetujui"));
        Assert.Equal(HttpStatusCode.OK, app2Res.StatusCode);
        var app2 = (await app2Res.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;

        Assert.Equal(2, app2.CurrentApprovedRevisionNumber);
        var rev1Final = app2.Revisions.Single(r => r.RevisionNumber == 1);
        var rev2Final = app2.Revisions.Single(r => r.RevisionNumber == 2);
        Assert.Equal(SalesOrderRevisionStatuses.Superseded, rev1Final.Status);
        Assert.Equal(rev1Hash, rev1Final.SnapshotHash);
        Assert.Equal(222_000m, rev1Final.GrandTotalAmount);
        Assert.Equal(333_000m, rev2Final.GrandTotalAmount);
        Assert.NotNull(rev2Final.DiffFromBaseline);
        Assert.Equal(111_000m, rev2Final.DiffFromBaseline!.GrandTotalDelta);
        Assert.Equal(5m, rev2Final.DiffFromBaseline.OrderedBaseQtyDelta);
        Assert.Contains(rev2Final.DiffFromBaseline.LineChanges, c => c.ChangeKind == SoDiffChangeKinds.Modified);
    }

    [Fact]
    public async Task S12_API_04_NegativeCasesOd09DiscountsTaxDirectionDownstreamFulfillmentCloseAndCrossTenantRbac()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var seller = await CreateSignedInClientAsync(factory, world.SalesOfficerA1Email, world.Password);
        using var approver = await CreateSignedInClientAsync(factory, world.ApproverA1Email, world.Password);
        using var readerA2 = await CreateSignedInClientAsync(factory, world.ReaderA2Email, world.Password);
        using var noSalesB1 = await CreateSignedInClientAsync(factory, world.MemberB1NoSalesEmail, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}";
        var baseB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}";

        // 1. Ineligible supplier-only partner -> 409
        var badCust = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.SupplierOnlyAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 0m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, badCust.StatusCode);

        // 2. Non-sales purchase-only item -> 409
        var badItem = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemPurchaseOnlyAId, world.PcsUomAId, 1m, 10_000m, 0m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, badItem.StatusCode);

        // 3. INPUT tax code on sales order -> 409 tax_code_direction_not_output
        var badTax = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 0m, world.PpnInTaxA1Id)]));
        Assert.Equal(HttpStatusCode.Conflict, badTax.StatusCode);

        // 4. OD-09 discount > 0 & credit limit override -> 422
        var badDiscount = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 1_000m, null)]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badDiscount.StatusCode);

        var badCredit = await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, true, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 0m, null)]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badCredit.StatusCode);

        // 5. Downstream reservation blocks cancellation & downward reduction -> 409, while short-close -> 200
        var soDto = (await (await seller.PostAsJsonAsync($"{baseA1}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto("SO-L001", world.ItemStockAId, world.PcsUomAId, 10m, 20_000m, 0m, world.PpnOutTaxA1Id)]))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var rev1Id = soDto.Revisions[0].RevisionId;
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{rev1Id:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{rev1Id:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "OK"))).StatusCode);

        // Simulate S13 downstream reservation on DB directly (header + line fulfillment counter)
        await using (var mDb = fx.CreateMigratorDbContext())
        {
            var soEntity = await mDb.SalesOrders.SingleAsync(x => x.SalesOrderId == soDto.SalesOrderId);
            var soLine = await mDb.SalesOrderLines.SingleAsync(x => x.RevisionId == rev1Id);
            soEntity.ApplyFulfillmentCounters(6m, 0m, 0m, DateTimeOffset.UtcNow);
            soLine.CarryForwardFulfillmentCounters(6m, 0m, 0m);
            await mDb.SaveChangesAsync();
        }

        var cancelBlocked = await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/cancel", new CloseOrCancelSoRequest("Batal coba"));
        Assert.Equal(HttpStatusCode.Conflict, cancelBlocked.StatusCode);

        // Downward amendment below 6 PCS reserved blocked (409)
        var am = (await (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions", new CreateSoAmendmentRequest("Turun qty"))).Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var amRevId = am.Revisions.Single(r => r.RevisionNumber == 2).RevisionId;
        var reduceBlocked = await seller.PutAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{amRevId:D}",
            new UpdateSalesOrderRevisionRequest(
                world.CustomerAllAId, new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, "Turun ke 4", null,
                [new SoLineInputDto("SO-L001", world.ItemStockAId, world.PcsUomAId, 4m, 20_000m, 0m, world.PpnOutTaxA1Id)]));
        Assert.Equal(HttpStatusCode.Conflict, reduceBlocked.StatusCode);

        // Increase qty to 12 (>= 6 reserved), submit & approve succeeds, then short-close succeeds
        var growOk = await seller.PutAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{amRevId:D}",
            new UpdateSalesOrderRevisionRequest(
                world.CustomerAllAId, new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, "Naik ke 12", null,
                [new SoLineInputDto("SO-L001", world.ItemStockAId, world.PcsUomAId, 12m, 20_000m, 0m, world.PpnOutTaxA1Id)]));
        Assert.Equal(HttpStatusCode.OK, growOk.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{amRevId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/revisions/{amRevId:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "OK 12"))).StatusCode);

        var closeRes = await seller.PostAsJsonAsync($"{baseA1}/sales-orders/{soDto.SalesOrderId:D}/close", new CloseOrCancelSoRequest("Sisa ditutup resmi"));
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);
        var closedDto = (await closeRes.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        Assert.Equal(SalesOrderStatuses.Closed, closedDto.Status);
        Assert.Equal(SalesFulfillmentProgressStatuses.ShortClosed, closedDto.FulfillmentProgress);

        // 6. RBAC & Cross-Tenant Isolation
        Assert.Equal(HttpStatusCode.OK, (await readerA2.GetAsync($"{baseA2}/sales-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerA2.PostAsJsonAsync($"{baseA2}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerAllAId, "IDR", new DateOnly(2026, 11, 15), null, "Addr", 14, null, false, null,
            [new SoLineInputDto(null, world.ItemStockAId, world.PcsUomAId, 1m, 10_000m, 0m, null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noSalesB1.GetAsync($"{baseB1}/sales-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync($"{baseB1}/sales-orders")).StatusCode);
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

    private static async Task<S12SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (sellerId, sellerEmail, _) = await fx.CreateTestUserAsync("seller.a1.s12@test.local", "Dewi Sales", pw);
        var (approverId, approverEmail, _) = await fx.CreateTestUserAsync("approver.a1.s12@test.local", "Budi Approver", pw);
        var (dualId, dualEmail, _) = await fx.CreateTestUserAsync("dual.a1.s12@test.local", "Sari Dual", pw);
        var (readerId, readerEmail, _) = await fx.CreateTestUserAsync("reader.a2.s12@test.local", "Andi Reader", pw);
        var (memberBId, memberBEmail, _) = await fx.CreateTestUserAsync("nosales.b1.s12@test.local", "Dedi Member", pw);
        var (foreignMgrId, foreignMgrEmail, _) = await fx.CreateTestUserAsync("mgr.b1.s12@test.local", "Samudra Mgr", pw);

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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, sellerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, approverId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, dualId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, memberBId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignMgrId, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, sellerId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, sellerId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, sellerId, PermissionCodes.SalesManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approverId, PermissionCodes.SalesApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualId, PermissionCodes.SalesManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, dualId, PermissionCodes.SalesApprove, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readerId, PermissionCodes.SalesRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, memberBId, PermissionCodes.WorkspaceView, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignMgrId, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignMgrId, PermissionCodes.SalesRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignMgrId, PermissionCodes.SalesManage, now, null, now));

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA2Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantBId, entityB1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now));

        var custAllA = Partner.Create(Guid.NewGuid(), tenantAId, "CUST-ALL", "PT Graha Ritel Nusantara", "Graha Ritel", false, true, "01.777.332.1-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var supOnlyA = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-ONLY", "PT Pemasok Saja", "Pemasok", true, false, null, TaxPkpStatuses.NonPkp, EntityScopeModes.AllEntities, now);
        var custB = Partner.Create(Guid.NewGuid(), tenantBId, "SMDR-CUST", "PT Pelayaran Lintas", "Pelayaran", false, true, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(custAllA, supOnlyA, custB);

        var pcsA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var boxA = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "BOX", "Box", 0, now);
        var pcsB = UnitOfMeasure.Create(Guid.NewGuid(), tenantBId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.AddRange(pcsA, boxA, pcsB);

        var itemStockA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-STK", "Karton Lipat", ItemKinds.StockedGood, pcsA.UomId, boxA.UomId, pcsA.UomId, EntityScopeModes.AllEntities, now);
        var itemPurchOnlyA = Item.Create(Guid.NewGuid(), tenantAId, "ITM-PO", "Bahan Beli Saja", ItemKinds.StockedGood, pcsA.UomId, pcsA.UomId, pcsA.UomId, EntityScopeModes.RestrictedEntities, now);
        db.Items.AddRange(itemStockA, itemPurchOnlyA);

        db.ItemUomConversions.Add(ItemUom.Create(Guid.NewGuid(), tenantAId, itemStockA.ItemId, boxA.UomId, 24m, now));
        db.ItemEntityPolicies.Add(ItemEntityPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, itemPurchOnlyA.ItemId, purchaseAllowed: true, salesAllowed: false, "No sales", now));

        var pos = StockPosition.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemStockA.ItemId, pcsA.UomId, now);
        pos.ApplyInboundReceipt(50m, StockStatuses.Unrestricted, now);
        db.StockPositions.Add(pos);

        var ppnOutId = Guid.NewGuid();
        var ppnInId = Guid.NewGuid();
        db.TaxCodes.AddRange(
            TaxCode.Create(ppnOutId, tenantAId, entityA1Id, "PPN-OUT-11", "PPN Keluaran 11%", TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, null, new DateOnly(2025, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, "OD-07 Output", now),
            TaxCode.Create(ppnInId, tenantAId, entityA1Id, "PPN-IN-11", "PPN Masukan 11%", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, null, new DateOnly(2025, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, "OD-07 Input", now));

        await db.SaveChangesAsync();

        return new S12SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            sellerId, sellerEmail, approverId, approverEmail, dualId, dualEmail,
            readerId, readerEmail, memberBId, memberBEmail, foreignMgrId, foreignMgrEmail,
            custAllA.PartnerId, supOnlyA.PartnerId, custB.PartnerId,
            pcsA.UomId, boxA.UomId, itemStockA.ItemId, itemPurchOnlyA.ItemId,
            ppnOutId, ppnInId, pw);
    }
}
