using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S10SupplierReturnsPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S10World(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid BuyerId,
        string BuyerEmail,
        Guid ApClerkId,
        string ApClerkEmail,
        Guid ControllerId,
        string ControllerEmail,
        Guid ForeignId,
        string ForeignEmail,
        Guid SupplierPkpId,
        Guid PcsUomId,
        Guid ItemSealId,
        Guid ItemGasketId,
        Guid ItemPphId,
        Guid PpnInputId,
        Guid Pph23Id,
        string Password);

    [Fact]
    public async Task S10_PG_01_ForcedRlsMissingContextAndAppendOnlyImmutability()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var retBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/returns";

        // Seed an executed M1 + ReplacementAuthorization AND an executed M3 + SupplierCredit
        var (po1, _, grn1, _) = await RecordArrivalAndDockCheckAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemSealId, world.PcsUomId,
            10m, 75_000m, releasedQty: 7m, rejectedQty: 3m,
            new DateOnly(2026, 11, 8), "SJ-PG01-M1", "idem-pg01-m1");

        var m1Create = await buyer.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            po1.PurchaseOrderId,
            grn1.GoodsReceiptId,
            null,
            null,
            new DateOnly(2026, 11, 8),
            SupplierReturnReasonCodes.DamagedOrDefective,
            "TRK-01",
            null,
            "PG01 M1 replacement",
            [new ReturnLineInputRequest(grn1.Lines[0].GoodsReceiptLineId, null, null, null, 3m)]));
        Assert.Equal(HttpStatusCode.Created, m1Create.StatusCode);
        var m1Ret = (await m1Create.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync($"{retBase}/{m1Ret.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{m1Ret.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approved M1"))).StatusCode);
        var m1Exec = await buyer.PostAsJsonAsync($"{retBase}/{m1Ret.ReturnHeader.SupplierReturnId:D}/execute", new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 8), "exec-pg01-m1"));
        Assert.Equal(HttpStatusCode.OK, m1Exec.StatusCode);

        var (accLine, billId, billLineId) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemGasketId, world.PcsUomId,
            10m, 100_000m, world.PpnInputId, null,
            new DateOnly(2026, 11, 9), "SJ-PG01-M3", "INV-PG01-M3", "idem-pg01-m3");

        var m3Create = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLine.PurchaseOrderId,
            accLine.GoodsReceiptId,
            accLine.ReceiptAccrualId,
            billId,
            new DateOnly(2026, 11, 10),
            SupplierReturnReasonCodes.WrongItemOrSpec,
            "TRK-02",
            "NR-2026-PG01",
            "PG01 M3 credit",
            [new ReturnLineInputRequest(accLine.GoodsReceiptLineId, accLine.ValuedAcceptanceLineId, accLine.ReceiptAccrualLineId, billLineId, 4m)]));
        Assert.Equal(HttpStatusCode.Created, m3Create.StatusCode);
        var m3Ret = (await m3Create.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{retBase}/{m3Ret.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{m3Ret.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approved M3"))).StatusCode);
        var m3Exec = await controller.PostAsJsonAsync($"{retBase}/{m3Ret.ReturnHeader.SupplierReturnId:D}/execute", new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 10), "exec-pg01-m3"));
        Assert.Equal(HttpStatusCode.OK, m3Exec.StatusCode);
        var m3ExecDto = (await m3Exec.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var executedReturnId = m3ExecDto.ReturnHeader.SupplierReturnId;
        var executedLineId = m3ExecDto.Lines[0].SupplierReturnLineId;
        var creditId = m3ExecDto.SupplierCredit!.SupplierCreditId;

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        // 1. All 8 S10 tables enforce ENABLE + FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = appConn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'procurement' AND c.relname IN (
                        'return_sequences', 'replacement_sequences', 'supplier_returns', 'supplier_return_lines', 'replacement_authorizations'))
                    OR (n.nspname = 'ap' AND c.relname IN ('credit_sequences', 'supplier_credits', 'supplier_credit_lines'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(8L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id denies reads (0 rows) and writes (42501)
        foreach (var tbl in new[]
        {
            "procurement.return_sequences", "procurement.replacement_sequences", "procurement.supplier_returns",
            "procurement.supplier_return_lines", "procurement.replacement_authorizations",
            "ap.credit_sequences", "ap.supplier_credits", "ap.supplier_credit_lines"
        })
        {
            await using var cmd = appConn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM {tbl};";
            Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
        }

        await using (var noCtxWrite = appConn.CreateCommand())
        {
            noCtxWrite.CommandText = "INSERT INTO procurement.return_sequences (tenant_id, legal_entity_id, fiscal_year, last_number) VALUES (@t, @e, 2026, 1);";
            noCtxWrite.Parameters.AddWithValue("t", world.TenantBId);
            noCtxWrite.Parameters.AddWithValue("e", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => noCtxWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Tenant B cannot read Tenant A S10 records nor insert into Tenant A
        await using (var txB = await appConn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(appConn, txB, world.TenantBId);

            foreach (var tbl in new[] { "procurement.supplier_returns", "procurement.replacement_authorizations", "ap.supplier_credits", "ap.supplier_credit_lines" })
            {
                await using var q = appConn.CreateCommand();
                q.Transaction = txB;
                q.CommandText = $"SELECT count(*) FROM {tbl} WHERE tenant_id = @ta;";
                q.Parameters.AddWithValue("ta", world.TenantAId);
                Assert.Equal(0L, Convert.ToInt64(await q.ExecuteScalarAsync()));
            }

            await AssertPgErrorInSavepointAsync(appConn, txB,
                $"INSERT INTO procurement.return_sequences (tenant_id, legal_entity_id, fiscal_year, last_number) VALUES ('{world.TenantAId:D}', '{world.EntityA1Id:D}', 2027, 1);",
                "42501");
        }

        // 4. Under Tenant A runtime role athera_app: append-only grants deny UPDATE/DELETE on ap.supplier_credits* (42501),
        //    and DB triggers block UPDATE/DELETE on EXECUTED procurement.supplier_returns* (55000).
        await using (var txA = await appConn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(appConn, txA, world.TenantAId);

            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE ap.supplier_credits SET total_credit_amount = 1 WHERE supplier_credit_id = '{creditId:D}';",
                "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM ap.supplier_credits WHERE supplier_credit_id = '{creditId:D}';",
                "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE ap.supplier_credit_lines SET credited_base_quantity = 99 WHERE supplier_credit_id = '{creditId:D}';",
                "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM ap.supplier_credit_lines WHERE supplier_credit_id = '{creditId:D}';",
                "42501");

            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE procurement.supplier_returns SET notes = 'tampered' WHERE supplier_return_id = '{executedReturnId:D}';",
                "55000");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM procurement.supplier_returns WHERE supplier_return_id = '{executedReturnId:D}';",
                "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE procurement.supplier_return_lines SET returned_base_quantity = 1 WHERE supplier_return_line_id = '{executedLineId:D}';",
                "55000");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM procurement.supplier_return_lines WHERE supplier_return_line_id = '{executedLineId:D}';",
                "55000");
        }

        // Also prove migrator trigger blocks ap.supplier_credits and EXECUTED supplier_returns mutations even for owner (55000)
        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using (var migTx = await migConn.BeginTransactionAsync())
        {
            await AssertPgErrorInSavepointAsync(migConn, migTx,
                $"UPDATE ap.supplier_credits SET total_credit_amount = 1 WHERE supplier_credit_id = '{creditId:D}';",
                "55000");
            await AssertPgErrorInSavepointAsync(migConn, migTx,
                $"DELETE FROM ap.supplier_credit_lines WHERE supplier_credit_id = '{creditId:D}';",
                "55000");
            await AssertPgErrorInSavepointAsync(migConn, migTx,
                $"DELETE FROM procurement.supplier_returns WHERE supplier_return_id = '{executedReturnId:D}';",
                "55000");
        }
    }

    [Fact]
    public async Task S10_PG_02_MigrationM10DownToM09AndUpRoundTrip()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        await SeedWorldAsync(fx);

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261118034334_M09_SupplierInvoiceThreeWayMatchApAndPriceDifference");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCmd = conn.CreateCommand();
            downCmd.CommandText = """
                SELECT count(*)
                FROM information_schema.tables
                WHERE (table_schema = 'procurement' AND table_name IN (
                        'return_sequences', 'replacement_sequences', 'supplier_returns', 'supplier_return_lines', 'replacement_authorizations'))
                   OR (table_schema = 'ap' AND table_name IN ('credit_sequences', 'supplier_credits', 'supplier_credit_lines'));
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCmd.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCmd = conn.CreateCommand();
            upCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'procurement' AND c.relname IN (
                        'return_sequences', 'replacement_sequences', 'supplier_returns', 'supplier_return_lines', 'replacement_authorizations'))
                    OR (n.nspname = 'ap' AND c.relname IN ('credit_sequences', 'supplier_credits', 'supplier_credit_lines'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(8L, Convert.ToInt64(await upCmd.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S10_API_01_M1UnvaluedReplacementZeroGlM2UnbilledCompensationM3BilledCreditNotaReturAndM4ToM7BlocksAndSavepointAtomicity()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var invBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";
        var apBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payables";
        var glBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";
        var retBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/returns";

        // === CASE M1: Unvalued Dock Rejection + Linked Replacement (Zero GL on return, Positive Inventory on replacement Gate 2) ===
        var (po1, poLine1, grn1, grLine1) = await RecordArrivalAndDockCheckAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemSealId, world.PcsUomId,
            10m, 75_000m, releasedQty: 7m, rejectedQty: 3m,
            new DateOnly(2026, 11, 5), "SJ-M1-01", "idem-m1-01");

        // Accept the 7 released units -> 7 * 75,000 = 525,000
        Assert.Equal(HttpStatusCode.Created, (await buyer.PostAsJsonAsync($"{rcvBase}/receipts/{grn1.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            "idem-m1-acc1", new DateOnly(2026, 11, 5), StockStatuses.Unrestricted, "Accept 7 released",
            [new AcceptReceiptLineRequest(grLine1.GoodsReceiptLineId, 7m)]))).StatusCode);

        // Ordinary arrival for 1 unit (without ReplacementAuthorizationId) still allowed for the 3 rejected unvalued capacity BEFORE M1 replacement reserves it OR check AFTER M1!
        var glBeforeM1 = await GetJournalCountAsync(controller, glBase);

        var m1Create = await buyer.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            po1.PurchaseOrderId,
            grn1.GoodsReceiptId,
            null,
            null,
            new DateOnly(2026, 11, 5),
            SupplierReturnReasonCodes.DamagedOrDefective,
            "TRK-M1",
            null,
            "M1 3 defective units",
            [new ReturnLineInputRequest(grLine1.GoodsReceiptLineId, null, null, null, 3m)]));
        Assert.Equal(HttpStatusCode.Created, m1Create.StatusCode);
        var m1Dto = (await m1Create.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync($"{retBase}/{m1Dto.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{m1Dto.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approved M1"))).StatusCode);
        var m1ExecRes = await buyer.PostAsJsonAsync($"{retBase}/{m1Dto.ReturnHeader.SupplierReturnId:D}/execute", new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 5), "exec-m1-01"));
        Assert.Equal(HttpStatusCode.OK, m1ExecRes.StatusCode);
        var m1Exec = (await m1ExecRes.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;

        Assert.Null(m1Exec.ReturnHeader.GlJournalId);
        Assert.Null(m1Exec.ReturnHeader.StockMovementId);
        Assert.Equal(glBeforeM1, await GetJournalCountAsync(controller, glBase)); // Zero GL!
        var rpa = Assert.Single(m1Exec.ReplacementAuthorizations);
        Assert.Equal(3m, rpa.AuthorizedBaseQuantity);
        Assert.Equal(0m, rpa.CommercialIncrementalUnitPrice);
        Assert.Equal(75_000m, rpa.AuthorizedInventoryUnitCostBase);

        // No free extra quantity can be released on ordinary PO arrival once RPA reserves remaining 3 units (OD-01 hold + block).
        var freeExtraRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            po1.PurchaseOrderId, "SJ-FREE-EXTRA", "idem-free-extra", new DateOnly(2026, 11, 5), "Dock", null, null, null,
            [new ArrivalLineRequest(poLine1.PoLineId, world.PcsUomId, 1m, false, DiscrepancyReasonCodes.None, null, null)]));
        Assert.Equal(HttpStatusCode.Created, freeExtraRes.StatusCode);
        var freeExtraGrn = (await freeExtraRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;
        Assert.Equal(1m, freeExtraGrn.Lines[0].OverdeliveryExcessBaseQuantity);
        Assert.Equal(DockCustodyStatuses.DockHeld, freeExtraGrn.Lines[0].CustodyStatus);

        var freeExtraReleaseRes = await buyer.PostAsJsonAsync(
            $"{rcvBase}/receipts/{freeExtraGrn.GoodsReceiptId:D}/lines/{freeExtraGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest("idem-free-extra-rel", 0m, 0m, 1m, 0m, DiscrepancyReasonCodes.None, "Attempt unauthorized release", null));
        Assert.Equal(HttpStatusCode.Conflict, freeExtraReleaseRes.StatusCode);
        Assert.Contains("od01_overdelivery_release_blocked", await freeExtraReleaseRes.Content.ReadAsStringAsync());

        // Replacement arrival above remaining RPA entitlement is also rejected.
        var overRepRes = await buyer.PostAsJsonAsync($"{retBase}/replacements/{rpa.ReplacementAuthorizationId:D}/receive", new ReceiveReplacementRequest(
            world.PcsUomId, 4m, new DateOnly(2026, 11, 6), "SJ-RPA-OVER", "idem-rpa-over", "Dermaga A", null, null, "Over entitlement", false));
        Assert.Equal(HttpStatusCode.Conflict, overRepRes.StatusCode);
        Assert.Contains("replacement_entitlement_exceeded", await overRepRes.Content.ReadAsStringAsync());

        // Linked replacement arrival succeeds, dock checks 3 PCS, Gate 2 values at 75,000 * 3 = 225,000!
        var repArrRes = await buyer.PostAsJsonAsync($"{retBase}/replacements/{rpa.ReplacementAuthorizationId:D}/receive", new ReceiveReplacementRequest(
            world.PcsUomId, 3m, new DateOnly(2026, 11, 6), "SJ-RPA-01", "idem-rpa-arr-01", "Dermaga A", null, null, "Replacement arrival", false));
        Assert.Equal(HttpStatusCode.Created, repArrRes.StatusCode);
        var repGrn = (await repArrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;
        Assert.Equal(rpa.ReplacementAuthorizationId, repGrn.ReplacementAuthorizationId);
        Assert.Equal(rpa.AuthorizationNumber, repGrn.ReplacementAuthorizationNumber);

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{rcvBase}/receipts/{repGrn.GoodsReceiptId:D}/lines/{repGrn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest("idem-rpa-dc-01", 0m, 0m, 3m, 0m, DiscrepancyReasonCodes.None, "Replacement clean", null))).StatusCode);

        var repAccRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts/{repGrn.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            "idem-rpa-acc-01", new DateOnly(2026, 11, 6), StockStatuses.Unrestricted, "Gate 2 replacement acceptance",
            [new AcceptReceiptLineRequest(repGrn.Lines[0].GoodsReceiptLineId, 3m)]));
        Assert.Equal(HttpStatusCode.Created, repAccRes.StatusCode);
        var repAccDto = (await repAccRes.Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions))!;
        Assert.Equal(225_000m, repAccDto.TotalAcceptedAmount);

        var m1CheckOverview = (await (await controller.GetAsync($"{retBase}/overview")).Content.ReadFromJsonAsync<ReturnsOverviewResponse>(JsonOptions))!;
        var rpaDone = m1CheckOverview.ReplacementAuthorizations.Single(a => a.ReplacementAuthorizationId == rpa.ReplacementAuthorizationId);
        Assert.Equal(ReplacementAuthorizationStatuses.Fulfilled, rpaDone.Status);
        Assert.Equal(0m, rpaDone.RemainingBaseQuantity);

        // === CASE M2: Valued Unbilled Return (Stock + GRNI Reversal + PO reversal + Closed-Bill Capacity Conservation + M7 Closed Period) ===
        var (po2, poLine2, accLine2) = await AcceptPoAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemGasketId, world.PcsUomId,
            10m, 120_000m, new DateOnly(2026, 11, 7), "SJ-M2-01", "idem-m2-rcv");

        var m2Create = await buyer.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedUnbilledReturn,
            ReturnResolutionModes.CloseWithoutReplacement,
            po2.PurchaseOrderId,
            accLine2.GoodsReceiptId,
            accLine2.ReceiptAccrualId,
            null,
            new DateOnly(2026, 11, 8),
            SupplierReturnReasonCodes.OverdeliveryReturn,
            "TRK-M2",
            null,
            "M2 4 units unbilled return",
            [new ReturnLineInputRequest(accLine2.GoodsReceiptLineId, accLine2.ValuedAcceptanceLineId, accLine2.ReceiptAccrualLineId, null, 4m)]));
        Assert.Equal(HttpStatusCode.Created, m2Create.StatusCode);
        var m2Dto = (await m2Create.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync($"{retBase}/{m2Dto.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{m2Dto.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approved M2"))).StatusCode);

        // M7: Execute against CLOSED October period -> 409 period_closed, nothing posted
        var m7Exec = await buyer.PostAsJsonAsync($"{retBase}/{m2Dto.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 10, 15), "exec-m7-closed"));
        Assert.Equal(HttpStatusCode.Conflict, m7Exec.StatusCode);
        Assert.Contains("period_closed", await m7Exec.Content.ReadAsStringAsync());

        // Now execute in OPEN November period
        var m2ExecRes = await buyer.PostAsJsonAsync($"{retBase}/{m2Dto.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 8), "exec-m2-open"));
        Assert.Equal(HttpStatusCode.OK, m2ExecRes.StatusCode);
        var m2Exec = (await m2ExecRes.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(480_000m, m2Exec.ReturnHeader.TotalGrniReversalAmount);
        Assert.Equal(480_000m, m2Exec.ReturnHeader.TotalInventoryCreditAmount);

        var m2Jrn = (await (await controller.GetAsync($"{glBase}/journals/{m2Exec.ReturnHeader.GlJournalId!.Value:D}"))
            .Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions))!;
        Assert.Equal(480_000m, m2Jrn.Lines.Where(l => l.AccountCode == "2110").Sum(l => l.DebitAmount));
        Assert.Equal(480_000m, m2Jrn.Lines.Where(l => l.AccountCode == "1300").Sum(l => l.CreditAmount));

        // Verify returned quantity cannot be billed later (attempt billing 10 when only 6 remain open -> 409!)
        var billOverRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po2.PurchaseOrderId, "INV-M2-OVER", null, null,
            new DateOnly(2026, 11, 9), new DateOnly(2026, 12, 9), "IDR", 1_332_000m, null,
            [new BillLineInputRequest(accLine2.ReceiptAccrualLineId, 10m, 120_000m, world.PpnInputId, null, null)]));
        // Submit or preview blocks > 6 open unbilled capacity
        if (billOverRes.IsSuccessStatusCode)
        {
            var overBill = (await billOverRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
            var submitOver = await apClerk.PostAsJsonAsync($"{apBase}/bills/{overBill.Bill.SupplierBillId:D}/submit", new { });
            Assert.Equal(HttpStatusCode.Conflict, submitOver.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, billOverRes.StatusCode);
        }

        // === CASE M3 + M4 + M5 + M6 + Savepoint Fault ===
        var (accLine3, bill3Id, billLine3Id) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemSealId, world.PcsUomId,
            10m, 75_000m, world.PpnInputId, null,
            new DateOnly(2026, 11, 9), "SJ-M3-01", "INV-M3-01", "idem-m3-01");

        // M4: Replacement requested on billed invoice -> blocked fail-closed on submit (409 od05_billed_replacement_policy_blocked)
        var m4DraftRes = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ReplacementOnly,
            accLine3.PurchaseOrderId,
            accLine3.GoodsReceiptId,
            accLine3.ReceiptAccrualId,
            bill3Id,
            new DateOnly(2026, 11, 10),
            SupplierReturnReasonCodes.DamagedOrDefective,
            null,
            null,
            "M4 test",
            [new ReturnLineInputRequest(accLine3.GoodsReceiptLineId, accLine3.ValuedAcceptanceLineId, accLine3.ReceiptAccrualLineId, billLine3Id, 2m)]));
        Assert.Equal(HttpStatusCode.Created, m4DraftRes.StatusCode);
        var m4Draft = (await m4DraftRes.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var m4Sub = await apClerk.PostAsJsonAsync($"{retBase}/{m4Draft.ReturnHeader.SupplierReturnId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, m4Sub.StatusCode);
        Assert.Contains("od05_billed_replacement_policy_blocked", await m4Sub.Content.ReadAsStringAsync());

        // M3 without Nota Retur fails on submit (409 od07_nota_retur_required)
        var m3DraftRes = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLine3.PurchaseOrderId,
            accLine3.GoodsReceiptId,
            accLine3.ReceiptAccrualId,
            bill3Id,
            new DateOnly(2026, 11, 10),
            SupplierReturnReasonCodes.CommercialAgreementReturn,
            "TRK-M3",
            TaxReturnNoteNumber: null,
            "Missing Nota Retur first",
            [new ReturnLineInputRequest(accLine3.GoodsReceiptLineId, accLine3.ValuedAcceptanceLineId, accLine3.ReceiptAccrualLineId, billLine3Id, 4m)]));
        Assert.Equal(HttpStatusCode.Created, m3DraftRes.StatusCode);
        var m3Draft = (await m3DraftRes.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;

        var missingNrSub = await apClerk.PostAsJsonAsync($"{retBase}/{m3Draft.ReturnHeader.SupplierReturnId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, missingNrSub.StatusCode);
        Assert.Contains("od07_nota_retur_required", await missingNrSub.Content.ReadAsStringAsync());

        // Update M3 draft with Nota Retur -> Submit -> Approve -> Test Savepoint Fault -> Then Execute cleanly!
        var m3Upd = await apClerk.PutAsJsonAsync($"{retBase}/{m3Draft.ReturnHeader.SupplierReturnId:D}", new UpdateSupplierReturnRequest(
            ReturnResolutionModes.ApCreditNote,
            new DateOnly(2026, 11, 10),
            SupplierReturnReasonCodes.CommercialAgreementReturn,
            "TRK-M3",
            "NR-2026-000888",
            "With Nota Retur",
            [new ReturnLineInputRequest(accLine3.GoodsReceiptLineId, accLine3.ValuedAcceptanceLineId, accLine3.ReceiptAccrualLineId, billLine3Id, 4m)]));
        Assert.Equal(HttpStatusCode.OK, m3Upd.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{retBase}/{m3Draft.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{m3Draft.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approved M3 credit"))).StatusCode);

        // Savepoint rollback check before real execution
        await using (var dbFault = fx.CreateMigratorDbContext())
        {
            await using var txFault = await dbFault.Database.BeginTransactionAsync();
            var faultOutcome = await SupplierReturnCoordinator.ExecuteAsync(
                dbFault,
                new ExecuteSupplierReturnCommand(
                    world.TenantAId,
                    world.EntityA1Id,
                    m3Draft.ReturnHeader.SupplierReturnId,
                    "IDR",
                    new DateOnly(2026, 11, 10),
                    "exec-fault-sp",
                    world.ControllerId,
                    DateTimeOffset.UtcNow,
                    FaultAfterSubledgerBeforeCommit: () => throw new InvalidOperationException("simulated_subledger_crash: Forced savepoint abort.")),
                CancellationToken.None);
            Assert.False(faultOutcome.Succeeded);
            Assert.Equal("simulated_subledger_crash", faultOutcome.ErrorCode);
            await txFault.CommitAsync(); // Commit outer tx so we prove savepoint truly rolled back PG rows!
        }

        // Verify zero orphaned SCN/JRN/MOV rows & return stays APPROVED!
        await using (var dbVerify = fx.CreateMigratorDbContext())
        {
            var rVerify = await dbVerify.SupplierReturns.AsNoTracking().SingleAsync(r => r.SupplierReturnId == m3Draft.ReturnHeader.SupplierReturnId);
            Assert.Equal(SupplierReturnStatuses.Approved, rVerify.Status);
            Assert.False(await dbVerify.SupplierCredits.AnyAsync(c => c.SupplierReturnId == m3Draft.ReturnHeader.SupplierReturnId));
        }

        // Now execute M3 cleanly
        var m3ExecRes = await controller.PostAsJsonAsync($"{retBase}/{m3Draft.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 10), "exec-m3-clean"));
        Assert.Equal(HttpStatusCode.OK, m3ExecRes.StatusCode);
        var m3Exec = (await m3ExecRes.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;

        Assert.NotNull(m3Exec.SupplierCredit);
        Assert.Equal(300_000m, m3Exec.SupplierCredit!.SubtotalCreditAmount);
        Assert.Equal(33_000m, m3Exec.SupplierCredit.PpnReversalAmount);
        Assert.Equal(333_000m, m3Exec.SupplierCredit.TotalCreditAmount);
        Assert.Equal("NR-2026-000888", m3Exec.SupplierCredit.TaxReturnNoteNumber);
        var revTax = Assert.Single(m3Exec.TaxDeterminations);
        Assert.Equal(TaxDeterminationRoles.PpnInputReversal, revTax.TaxRole);
        Assert.Equal(33_000m, revTax.TaxAmount);

        var m3Jrn = (await (await controller.GetAsync($"{glBase}/journals/{m3Exec.SupplierCredit.GlJournalId:D}"))
            .Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions))!;
        Assert.Equal(333_000m, m3Jrn.Lines.Where(l => l.AccountCode == "2100").Sum(l => l.DebitAmount));
        Assert.Equal(300_000m, m3Jrn.Lines.Where(l => l.AccountCode == "1300").Sum(l => l.CreditAmount));
        Assert.Equal(33_000m, m3Jrn.Lines.Where(l => l.AccountCode == "1410").Sum(l => l.CreditAmount));

        // Duplicate/over-credit attempt for 7 more units when only 6 remain -> blocked!
        var dupM3Res = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLine3.PurchaseOrderId,
            accLine3.GoodsReceiptId,
            accLine3.ReceiptAccrualId,
            bill3Id,
            new DateOnly(2026, 11, 10),
            SupplierReturnReasonCodes.CommercialAgreementReturn,
            null,
            "NR-2026-000889",
            "Over-credit check",
            [new ReturnLineInputRequest(accLine3.GoodsReceiptLineId, accLine3.ValuedAcceptanceLineId, accLine3.ReceiptAccrualLineId, billLine3Id, 7m)]));
        Assert.Equal(HttpStatusCode.Created, dupM3Res.StatusCode);
        var dupM3 = (await dupM3Res.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var dupM3Sub = await apClerk.PostAsJsonAsync($"{retBase}/{dupM3.ReturnHeader.SupplierReturnId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, dupM3Sub.StatusCode);

        // M6: Billed invoice carrying PPh 23 withholding -> 409 od07_withholding_return_reversal_blocked
        var (accLineWh, billWhId, billLineWhId) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemPphId, world.PcsUomId,
            10m, 100_000m, world.PpnInputId, world.Pph23Id,
            new DateOnly(2026, 11, 10), "SJ-M6-01", "INV-M6-01", "idem-m6-01");

        var m6Res = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLineWh.PurchaseOrderId,
            accLineWh.GoodsReceiptId,
            accLineWh.ReceiptAccrualId,
            billWhId,
            new DateOnly(2026, 11, 11),
            SupplierReturnReasonCodes.DamagedOrDefective,
            null,
            "NR-2026-M6",
            "M6 withholding block",
            [new ReturnLineInputRequest(accLineWh.GoodsReceiptLineId, accLineWh.ValuedAcceptanceLineId, accLineWh.ReceiptAccrualLineId, billLineWhId, 2m)]));
        Assert.Equal(HttpStatusCode.Created, m6Res.StatusCode);
        var m6Dto = (await m6Res.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var m6Sub = await apClerk.PostAsJsonAsync($"{retBase}/{m6Dto.ReturnHeader.SupplierReturnId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, m6Sub.StatusCode);
        Assert.Contains("od07_withholding_return_reversal_blocked", await m6Sub.Content.ReadAsStringAsync());

        // M5: Paid bill simulation -> 409 od05_paid_bill_return_policy_blocked
        var (accLinePaid, billPaidId, billLinePaidId) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemGasketId, world.PcsUomId,
            5m, 100_000m, world.PpnInputId, null,
            new DateOnly(2026, 11, 11), "SJ-M5-01", "INV-M5-01", "idem-m5-01");

        await using (var dbPay = fx.CreateMigratorDbContext())
        {
            await dbPay.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE ap.payable_items
                SET paid_amount = 100000,
                    remaining_open_amount = net_payable_amount - 100000,
                    status = 'PARTIALLY_PAID'
                WHERE tenant_id = {world.TenantAId}
                  AND legal_entity_id = {world.EntityA1Id}
                  AND supplier_bill_id = {billPaidId};
                """);
        }

        var m5Res = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLinePaid.PurchaseOrderId,
            accLinePaid.GoodsReceiptId,
            accLinePaid.ReceiptAccrualId,
            billPaidId,
            new DateOnly(2026, 11, 12),
            SupplierReturnReasonCodes.CommercialAgreementReturn,
            null,
            "NR-2026-M5",
            "M5 paid block",
            [new ReturnLineInputRequest(accLinePaid.GoodsReceiptLineId, accLinePaid.ValuedAcceptanceLineId, accLinePaid.ReceiptAccrualLineId, billLinePaidId, 1m)]));
        Assert.Equal(HttpStatusCode.Created, m5Res.StatusCode);
        var m5Dto = (await m5Res.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var m5Sub = await apClerk.PostAsJsonAsync($"{retBase}/{m5Dto.ReturnHeader.SupplierReturnId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.Conflict, m5Sub.StatusCode);
        Assert.Contains("od05_paid_bill_return_policy_blocked", await m5Sub.Content.ReadAsStringAsync());

        // Check Inventory movement shows PURCHASE_RETURN_OUTBOUND cleanly
        var invOverview = (await (await controller.GetAsync($"{invBase}/overview")).Content.ReadFromJsonAsync<InventoryOverviewResponse>(JsonOptions))!;
        Assert.Contains(invOverview.RecentMovements, m => m.MovementType == StockMovementTypes.PurchaseReturnOutbound);
    }

    [Fact]
    public async Task S10_API_02_ConcurrencyAndIdempotency()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer1 = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var buyer2 = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var retBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/returns";

        var (po1, _, grn1, grLine1) = await RecordArrivalAndDockCheckAsync(
            buyer1, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemSealId, world.PcsUomId,
            8m, 75_000m, releasedQty: 5m, rejectedQty: 3m,
            new DateOnly(2026, 11, 8), "SJ-CONC-M1", "idem-conc-m1");

        var createM1 = await buyer1.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            po1.PurchaseOrderId, grn1.GoodsReceiptId, null, null,
            new DateOnly(2026, 11, 8), SupplierReturnReasonCodes.DamagedOrDefective,
            null, null, "Concurrent return",
            [new ReturnLineInputRequest(grLine1.GoodsReceiptLineId, null, null, null, 3m)]));
        var ret1 = (await createM1.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await buyer1.PostAsJsonAsync($"{retBase}/{ret1.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{ret1.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Approve race test"))).StatusCode);

        // Race two concurrent Execute calls with different keys -> 1 succeeds (200), 1 fails (409 return_already_executed)
        var execTasks = await Task.WhenAll(
            buyer1.PostAsJsonAsync($"{retBase}/{ret1.ReturnHeader.SupplierReturnId:D}/execute", new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 8), "exec-race-1")),
            buyer2.PostAsJsonAsync($"{retBase}/{ret1.ReturnHeader.SupplierReturnId:D}/execute", new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 8), "exec-race-2")));

        var okExecs = execTasks.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        var confExecs = execTasks.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Single(okExecs);
        Assert.Single(confExecs);

        var wonDto = (await okExecs[0].Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        var winnerKey = ReferenceEquals(okExecs[0], execTasks[0]) ? "exec-race-1" : "exec-race-2";

        // Idempotent replay returns 200 with WasIdempotentReplay == true
        var replay = await buyer1.PostAsJsonAsync($"{retBase}/{ret1.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 8), winnerKey));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayDto = (await replay.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.True(replayDto.WasIdempotentReplay);
        Assert.Equal(wonDto.ReturnHeader.ReturnNumber, replayDto.ReturnHeader.ReturnNumber);

        // Race two concurrent linked replacement arrivals each claiming full 3 PCS -> 1 wins (201), 1 conflicts (409 replacement_entitlement_exceeded)
        var rpaId = wonDto.ReplacementAuthorizations[0].ReplacementAuthorizationId;
        var rpaTasks = await Task.WhenAll(
            buyer1.PostAsJsonAsync($"{retBase}/replacements/{rpaId:D}/receive", new ReceiveReplacementRequest(
                world.PcsUomId, 3m, new DateOnly(2026, 11, 9), "SJ-RPA-RACE-1", "idem-rpa-race-1", "Dock", null, null, null, false)),
            buyer2.PostAsJsonAsync($"{retBase}/replacements/{rpaId:D}/receive", new ReceiveReplacementRequest(
                world.PcsUomId, 3m, new DateOnly(2026, 11, 9), "SJ-RPA-RACE-2", "idem-rpa-race-2", "Dock", null, null, null, false)));

        Assert.Single(rpaTasks, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(rpaTasks, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task S10_API_03_FourEyesSodRbacReadOnlyEntityCrossTenant404AndAuditOutbox()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);
        using var foreign = await CreateSignedInClientAsync(factory, world.ForeignEmail, world.Password);

        var retBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/returns";
        var sbyRetBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/returns";

        var (po1, _, grn1, grLine1) = await RecordArrivalAndDockCheckAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemSealId, world.PcsUomId,
            5m, 75_000m, releasedQty: 3m, rejectedQty: 2m,
            new DateOnly(2026, 11, 8), "SJ-SOD-M1", "idem-sod-m1");

        // 1. Controller creates & submits M1 return, then attempts OD-02 self-approval -> 403 self_approval_forbidden
        var selfCreate = await buyer.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.CloseWithoutReplacement,
            po1.PurchaseOrderId, grn1.GoodsReceiptId, null, null,
            new DateOnly(2026, 11, 8), SupplierReturnReasonCodes.DamagedOrDefective, null, null, null,
            [new ReturnLineInputRequest(grLine1.GoodsReceiptLineId, null, null, null, 2m)]));
        Assert.Equal(HttpStatusCode.Created, selfCreate.StatusCode);
        var m1 = (await selfCreate.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync($"{retBase}/{m1.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);

        // Buyer cannot approve (missing procurement.approve / approval.decide -> 403)
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync(
            $"{retBase}/{m1.ReturnHeader.SupplierReturnId:D}/decision",
            new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Unprivileged approve attempt"))).StatusCode);

        // Controller submits own M3 draft and tries self-approval -> 403 self_approval_forbidden
        var (accLine, billId, billLineId) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemGasketId, world.PcsUomId,
            6m, 100_000m, world.PpnInputId, null,
            new DateOnly(2026, 11, 9), "SJ-SOD-M3", "INV-SOD-M3", "idem-sod-m3");

        var ctrlCreate = await controller.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLine.PurchaseOrderId, accLine.GoodsReceiptId, accLine.ReceiptAccrualId, billId,
            new DateOnly(2026, 11, 10), SupplierReturnReasonCodes.DamagedOrDefective, null, "NR-SOD-01", null,
            [new ReturnLineInputRequest(accLine.GoodsReceiptLineId, accLine.ValuedAcceptanceLineId, accLine.ReceiptAccrualLineId, billLineId, 2m)]));
        Assert.Equal(HttpStatusCode.Created, ctrlCreate.StatusCode);
        var ctrlRet = (await ctrlCreate.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{ctrlRet.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);

        var selfApprove = await controller.PostAsJsonAsync(
            $"{retBase}/{ctrlRet.ReturnHeader.SupplierReturnId:D}/decision",
            new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Attempt OD-02 self-approval"));
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);
        Assert.Contains("self_approval_forbidden", await selfApprove.Content.ReadAsStringAsync());

        // Reject by another approver? Let's test AP Clerk executing M3 (forbidden) after Controller approves Buyer/AP Clerk's M3!
        var apCreate = await apClerk.PostAsJsonAsync(retBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            accLine.PurchaseOrderId, accLine.GoodsReceiptId, accLine.ReceiptAccrualId, billId,
            new DateOnly(2026, 11, 10), SupplierReturnReasonCodes.DamagedOrDefective, null, "NR-SOD-02", null,
            [new ReturnLineInputRequest(accLine.GoodsReceiptLineId, accLine.ValuedAcceptanceLineId, accLine.ReceiptAccrualLineId, billLineId, 2m)]));
        Assert.Equal(HttpStatusCode.Created, apCreate.StatusCode);
        var apRet = (await apCreate.Content.ReadFromJsonAsync<SupplierReturnDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{retBase}/{apRet.ReturnHeader.SupplierReturnId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{retBase}/{apRet.ReturnHeader.SupplierReturnId:D}/decision", new DecideSupplierReturnRequest(ApprovalDecisions.Approved, "Controller approved"))).StatusCode);

        // AP Clerk lacks ap.post -> 403 Forbidden on execute
        Assert.Equal(HttpStatusCode.Forbidden, (await apClerk.PostAsJsonAsync(
            $"{retBase}/{apRet.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 10), "exec-apclerk-forbidden"))).StatusCode);

        // Controller executes M3 cleanly -> Audit & Outbox emitted
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{retBase}/{apRet.ReturnHeader.SupplierReturnId:D}/execute",
            new ExecuteSupplierReturnRequest(new DateOnly(2026, 11, 10), "exec-ctrl-ok"))).StatusCode);

        // 2. Read-only Entity A2 (NUSA-SBY): Controller can GET overview (200), but mutating POST returns 403
        Assert.Equal(HttpStatusCode.OK, (await controller.GetAsync($"{sbyRetBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PostAsJsonAsync(sbyRetBase, new CreateSupplierReturnRequest(
            SupplierReturnCaseTypes.ValuedUnbilledReturn, ReturnResolutionModes.CloseWithoutReplacement,
            accLine.PurchaseOrderId, accLine.GoodsReceiptId, accLine.ReceiptAccrualId, null,
            new DateOnly(2026, 11, 10), SupplierReturnReasonCodes.DamagedOrDefective, null, null, null,
            [new ReturnLineInputRequest(accLine.GoodsReceiptLineId, accLine.ValuedAcceptanceLineId, accLine.ReceiptAccrualLineId, null, 1m)]))).StatusCode);

        // 3. Cross-tenant hostile selector -> 404
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"{retBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"{retBase}/{apRet.ReturnHeader.SupplierReturnId:D}")).StatusCode);

        // 4. Verify AuditEvent & OutboxMessage persistence
        await using var db = fx.CreateMigratorDbContext();
        var events = await db.AuditEvents.AsNoTracking()
            .Where(a => a.TenantId == world.TenantAId && a.SubjectId == apRet.ReturnHeader.SupplierReturnId.ToString("D"))
            .Select(a => a.EventType)
            .ToListAsync();
        Assert.Contains("SUPPLIER_RETURN_DRAFT_CREATED", events);
        Assert.Contains("SUPPLIER_RETURN_SUBMITTED", events);
        Assert.Contains("SUPPLIER_RETURN_APPROVED", events);
        Assert.Contains("SUPPLIER_RETURN_EXECUTED", events);

        var outboxTopics = await db.OutboxMessages.AsNoTracking()
            .Where(o => o.TenantId == world.TenantAId && o.LegalEntityId == world.EntityA1Id)
            .Select(o => o.Topic)
            .ToListAsync();
        Assert.Contains("procurement.supplierreturn.submitted.v1", outboxTopics);
        Assert.Contains("procurement.supplierreturn.decided.v1", outboxTopics);
        Assert.Contains("procurement.supplierreturn.executed.v1", outboxTopics);
    }

    private static async Task<int> GetJournalCountAsync(HttpClient client, string glBase)
    {
        var paged = (await (await client.GetAsync($"{glBase}/journals")).Content.ReadFromJsonAsync<PagedJournalsResponse>(JsonOptions))!;
        return paged.TotalCount;
    }

    private static async Task<(PurchaseOrderDetailDto Po, PurchaseOrderLineDto PoLine, GoodsReceiptDetailDto Receipt, GoodsReceiptLineDto Line)> RecordArrivalAndDockCheckAsync(
        HttpClient buyer,
        HttpClient controller,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal orderedQty,
        decimal unitPrice,
        decimal releasedQty,
        decimal rejectedQty,
        DateOnly date,
        string deliveryNote,
        string idempBase)
    {
        var procBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/procurement";
        var rcvBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/receiving";

        var createRes = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            supplierId, "IDR", date, date.AddDays(14), "Gudang Jakarta", 30, null, null,
            [new PoLineInputDto("L001", itemId, uomId, orderedQty, unitPrice, null)]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draft = (await createRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null))).StatusCode);
        var appRes = await controller.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{draft.PurchaseOrderId:D}/revisions/{draft.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest(ApprovalDecisions.Approved, "Approved PO"));
        Assert.Equal(HttpStatusCode.OK, appRes.StatusCode);
        var approved = (await appRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;
        var poLine = approved.Revisions[0].Lines[0];

        var arrRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            approved.PurchaseOrderId, deliveryNote, $"{idempBase}-gr", date, "Dermaga A", null, null, null,
            [new ArrivalLineRequest(poLine.PoLineId, uomId, orderedQty, rejectedQty > 0m, rejectedQty > 0m ? DiscrepancyReasonCodes.WetOrDefectiveGoods : DiscrepancyReasonCodes.None, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = (await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;

        var dcRes = await buyer.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest(
                $"{idempBase}-dc", 0m, 0m, releasedQty, rejectedQty,
                rejectedQty > 0m ? DiscrepancyReasonCodes.WetOrDefectiveGoods : DiscrepancyReasonCodes.None,
                "Checked", null));
        Assert.Equal(HttpStatusCode.OK, dcRes.StatusCode);
        var updatedGrn = (await dcRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;
        return (approved, poLine, updatedGrn, updatedGrn.Lines[0]);
    }

    private static async Task<(PurchaseOrderDetailDto Po, PurchaseOrderLineDto PoLine, EligibleAccrualLineDto AccrualLine)> AcceptPoAsync(
        HttpClient buyer,
        HttpClient controller,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal qty,
        decimal unitPrice,
        DateOnly date,
        string deliveryNote,
        string idempBase)
    {
        var rcvBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/receiving";
        var apBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/payables";
        var (po, poLine, grn, line) = await RecordArrivalAndDockCheckAsync(
            buyer, controller, tenantId, entityId, supplierId, itemId, uomId, qty, unitPrice, qty, 0m, date, deliveryNote, idempBase);

        var accRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            $"{idempBase}-acc", date, StockStatuses.Unrestricted, "Gate 2 acceptance",
            [new AcceptReceiptLineRequest(line.GoodsReceiptLineId, qty)]));
        Assert.Equal(HttpStatusCode.Created, accRes.StatusCode);

        var overview = (await (await controller.GetAsync($"{apBase}/overview"))
            .Content.ReadFromJsonAsync<PayablesOverviewResponse>(JsonOptions))!;
        var accLine = overview.EligibleAccrualLines.Single(l => l.PoLineId == poLine.PoLineId);
        return (po, poLine, accLine);
    }

    private static async Task<(EligibleAccrualLineDto AccrualLine, Guid BillId, Guid BillLineId)> AcceptAndPostBillAsync(
        HttpClient buyer,
        HttpClient apClerk,
        HttpClient controller,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal qty,
        decimal unitPrice,
        Guid ppnTaxId,
        Guid? pphTaxId,
        DateOnly date,
        string deliveryNote,
        string invoiceNo,
        string idempBase)
    {
        var apBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/payables";
        var (po, _, accLine) = await AcceptPoAsync(buyer, controller, tenantId, entityId, supplierId, itemId, uomId, qty, unitPrice, date, deliveryNote, idempBase);

        var sub = qty * unitPrice;
        var ppn = decimal.Round(sub * 0.11m, 0, MidpointRounding.AwayFromZero);
        var pph = pphTaxId.HasValue ? decimal.Round(sub * 0.02m, 0, MidpointRounding.AwayFromZero) : 0m;
        var declaredGross = sub + ppn;

        var draftRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, invoiceNo, "010.001-26.90001", pph > 0m ? "BP-01" : null,
            date, date.AddDays(30), "IDR", declaredGross, "S10 bill fixture",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, qty, unitPrice, ppnTaxId, pphTaxId, null)]));
        Assert.Equal(HttpStatusCode.Created, draftRes.StatusCode);
        var draft = (await draftRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approved", false, false, null))).StatusCode);
        var postRes = await controller.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(date, $"{idempBase}-post"));
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var posted = (await postRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        return (accLine, posted.Bill.SupplierBillId, posted.Lines[0].SupplierBillLineId);
    }

    private static async Task AssertPgErrorInSavepointAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, string expectedSqlState)
    {
        await using var sp = conn.CreateCommand();
        sp.Transaction = tx;
        sp.CommandText = "SAVEPOINT s_err;";
        await sp.ExecuteNonQueryAsync();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(expectedSqlState, ex.SqlState);

        await using var rb = conn.CreateCommand();
        rb.Transaction = tx;
        rb.CommandText = "ROLLBACK TO SAVEPOINT s_err;";
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

    private static async Task<S10World> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (buyerId, buyerEmail, _) = await fx.CreateTestUserAsync("rina.s10@test.local", "Rina S10 Buyer", pw);
        var (apClerkId, apClerkEmail, _) = await fx.CreateTestUserAsync("siti.s10@test.local", "Siti S10 AP Clerk", pw);
        var (controllerId, controllerEmail, _) = await fx.CreateTestUserAsync("budi.s10@test.local", "Budi S10 Controller", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.s10@test.local", "Samudra Staff", pw);

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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, buyerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, apClerkId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, controllerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept,
            PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerId, perm, now, null, now));
        }

        foreach (var perm in RoleTemplates.GetPermissions(RoleTemplates.ApClerk))
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, apClerkId, perm, now, null, now));
        }

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove,
            PermissionCodes.ReceivingRead, PermissionCodes.InventoryRead,
            PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost,
            PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost,
            PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, controllerId, perm, now, null, now));
        }

        // Read-only Entity A2 (NUSA-SBY) for Controller
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.WorkspaceView, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.ProcurementRead, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.ApRead, now, null, now));

        // Tenant B foreign user
        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, perm, now, null, now));
        }

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.SupplierBillApprovalPolicyCode, "Bill Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, ReturnPolicyDefaults.SupplierReturnApprovalPolicyCode, "Return Approval", true, false, now));
        db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));
        db.InvoiceMatchPolicies.Add(InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03 Safe", true, now));

        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1410", "PPN Masukan", GlAccountTypes.Asset, GlAccountCategories.TaxReceivable, null, true, true, now);
        var apAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2100", "Hutang Usaha", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, null, true, true, now);
        var grniAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        var pphAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2220", "Hutang PPh 23", GlAccountTypes.Liability, GlAccountCategories.WithholdingTaxPayable, null, true, true, now);
        var cogsAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "Harga Pokok Penjualan", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, true, now);
        var ppvAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5200", "Selisih Harga Pembelian", GlAccountTypes.Expense, GlAccountCategories.PurchasePriceVariance, null, true, true, now);
        db.GlAccounts.AddRange(invAcc, ppnAcc, apAcc, grniAcc, pphAcc, cogsAcc, ppvAcc);

        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcc, "Inv", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grniAcc, "GRNI", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ApTradeControl, apAcc, "AP", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PurchasePriceVariance, ppvAcc, "PPV", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcc, "COGS", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnInputTax, ppnAcc, "PPN-In", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PphWithholdingPayable, pphAcc, "PPh23", controllerId, now));

        var ppnCode = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPN-IN-11", "PPN Masukan 11%", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, ppnAcc.AccountId, new DateOnly(2026, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, null, now);
        var pph23Code = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPH23-SVC-2", "PPh 23 Jasa 2%", TaxFamilies.Pph, TaxDirections.Withholding, TaxTreatmentClasses.WithholdingDeduction, 2m, 1, 1, false, true, pphAcc.AccountId, new DateOnly(2026, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, null, now);
        db.TaxCodes.AddRange(ppnCode, pph23Code);

        var supPkp = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-A", "PT Sumber Makmur PKP", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        db.Partners.Add(supPkp);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);

        var itemSeal = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S10-SEAL-01", "Seal Hidrolik Industrial", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        var itemGasket = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S10-GASKET-02", "Gasket PTFE 4 Inch", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        var itemPph = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S10-VALVE-PPH", "Katup Kontrol Ber-PPh", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Items.AddRange(itemSeal, itemGasket, itemPph);

        await db.SaveChangesAsync();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);
            await tx.CommitAsync();
        }

        return new S10World(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            buyerId, buyerEmail, apClerkId, apClerkEmail, controllerId, controllerEmail, foreignId, foreignEmail,
            supPkp.PartnerId, pcs.UomId, itemSeal.ItemId, itemGasket.ItemId, itemPph.ItemId,
            ppnCode.TaxCodeId, pph23Code.TaxCodeId, pw);
    }
}
