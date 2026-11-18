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

public sealed class S09SupplierInvoicePostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S09SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ApClerkId,
        string ApClerkEmail,
        Guid ControllerId,
        string ControllerEmail,
        Guid ProcurementOnlyId,
        string ProcurementOnlyEmail,
        Guid ForeignUserId,
        string ForeignUserEmail,
        Guid SupplierPkpId,
        Guid SupplierNoNpwpId,
        Guid PcsUomId,
        Guid ItemValveId,
        Guid ItemZeroCovId,
        Guid PpnInputId,
        Guid Pph23Id,
        Guid WriteOffAccountId,
        Guid SeedBillId,
        Guid SeedBillLineId,
        Guid SeedPayableId,
        Guid SeedEvalId,
        Guid SeedMatchId,
        Guid SeedClearId,
        Guid SeedAdjId,
        Guid SeedTaxDetId,
        string Password);

    [Fact]
    public async Task S09_PG_01_RlsAppendOnlyAndPostedBillImmutabilityTriggersEnforced()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx, seedPostedBill: true);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        // 1. All 11 S09 tables enforce ENABLE + FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'ap' AND c.relname IN (
                        'bill_sequences', 'payable_sequences', 'invoice_match_policies', 'supplier_bills',
                        'supplier_bill_lines', 'invoice_match_evaluations', 'supplier_bill_receipt_matches', 'payable_items'))
                    OR (n.nspname = 'gl' AND c.relname = 'receipt_accrual_clearings')
                    OR (n.nspname = 'inventory' AND c.relname = 'invoice_price_adjustments')
                    OR (n.nspname = 'tax' AND c.relname = 'tax_determinations')
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id denies reads (0 rows) and writes (42501)
        foreach (var table in new[]
        {
            "ap.invoice_match_policies",
            "ap.supplier_bills",
            "ap.supplier_bill_lines",
            "ap.invoice_match_evaluations",
            "ap.supplier_bill_receipt_matches",
            "ap.payable_items",
            "gl.receipt_accrual_clearings",
            "inventory.invoice_price_adjustments",
            "tax.tax_determinations"
        })
        {
            await using var readCmd = conn.CreateCommand();
            readCmd.CommandText = $"SELECT count(*) FROM {table};";
            Assert.Equal(0L, Convert.ToInt64(await readCmd.ExecuteScalarAsync()));
        }

        await using (var noCtxWrite = conn.CreateCommand())
        {
            noCtxWrite.CommandText = """
                INSERT INTO ap.bill_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@tid, @eid, 2026, 1);
                """;
            noCtxWrite.Parameters.AddWithValue("tid", world.TenantBId);
            noCtxWrite.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => noCtxWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Under Tenant B context: Tenant A AP/tax/clearing/adjustment rows are invisible & cross-tenant write denied (42501)
        await using (var txB = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, txB, world.TenantBId);

            foreach (var table in new[] { "ap.supplier_bills", "ap.payable_items", "inventory.invoice_price_adjustments", "tax.tax_determinations" })
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
                INSERT INTO ap.bill_sequences (tenant_id, legal_entity_id, fiscal_year, last_number)
                VALUES (@ta, @ea, 2026, 99);
                """;
            crossWrite.Parameters.AddWithValue("ta", world.TenantAId);
            crossWrite.Parameters.AddWithValue("ea", world.EntityA1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => crossWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 4. Least-privilege append-only tables reject UPDATE/DELETE (42501); POSTED bill & line immutability triggers block accounting mutations (55000) while allowing payment block updates
        await using (var txA = await conn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(conn, txA, world.TenantAId);

            var privStatements = new[]
            {
                $"UPDATE ap.invoice_match_evaluations SET bd_outcome = 'BLOCKED' WHERE evaluation_id = '{world.SeedEvalId:D}';",
                $"DELETE FROM ap.invoice_match_evaluations WHERE evaluation_id = '{world.SeedEvalId:D}';",
                $"UPDATE ap.supplier_bill_receipt_matches SET matched_base_quantity = 999 WHERE match_id = '{world.SeedMatchId:D}';",
                $"DELETE FROM ap.supplier_bill_receipt_matches WHERE match_id = '{world.SeedMatchId:D}';",
                $"UPDATE gl.receipt_accrual_clearings SET cleared_amount = 1 WHERE clearing_id = '{world.SeedClearId:D}';",
                $"DELETE FROM gl.receipt_accrual_clearings WHERE clearing_id = '{world.SeedClearId:D}';",
                $"UPDATE inventory.invoice_price_adjustments SET pnl_variance_amount = 1 WHERE adjustment_id = '{world.SeedAdjId:D}';",
                $"DELETE FROM inventory.invoice_price_adjustments WHERE adjustment_id = '{world.SeedAdjId:D}';",
                $"UPDATE tax.tax_determinations SET tax_amount = 1 WHERE determination_id = '{world.SeedTaxDetId:D}';",
                $"DELETE FROM tax.tax_determinations WHERE determination_id = '{world.SeedTaxDetId:D}';",
                $"DELETE FROM ap.supplier_bills WHERE supplier_bill_id = '{world.SeedBillId:D}';",
                $"DELETE FROM ap.payable_items WHERE payable_item_id = '{world.SeedPayableId:D}';"
            };

            foreach (var sql in privStatements)
            {
                await AssertPgErrorInSavepointAsync(conn, txA, sql, "42501");
            }

            // Trigger 1: POSTED bill header accounting fields immutable (SQLSTATE 55000)
            await AssertPgErrorInSavepointAsync(conn, txA, $"""
                UPDATE ap.supplier_bills
                SET lines_net_subtotal = lines_net_subtotal + 100
                WHERE supplier_bill_id = '{world.SeedBillId:D}';
                """, "55000");

            // Trigger 2: POSTED bill lines immutable on UPDATE and DELETE (SQLSTATE 55000)
            await AssertPgErrorInSavepointAsync(conn, txA, $"""
                UPDATE ap.supplier_bill_lines
                SET billed_base_quantity = 99
                WHERE supplier_bill_line_id = '{world.SeedBillLineId:D}';
                """, "55000");

            await AssertPgErrorInSavepointAsync(conn, txA, $"""
                DELETE FROM ap.supplier_bill_lines
                WHERE supplier_bill_line_id = '{world.SeedBillLineId:D}';
                """, "55000");

            // Payment block status toggle IS permitted on POSTED supplier_bills by trigger
            await using var allowCmd = conn.CreateCommand();
            allowCmd.Transaction = txA;
            allowCmd.CommandText = $"""
                UPDATE ap.supplier_bills
                SET payment_block_status = 'PAYMENT_BLOCKED',
                    payment_block_reason = 'Waiting hardcopy faktur',
                    version = version + 1
                WHERE supplier_bill_id = '{world.SeedBillId:D}';
                """;
            Assert.Equal(1, await allowCmd.ExecuteNonQueryAsync());
        }
    }

    [Fact]
    public async Task S09_PG_02_MigrationM09DownToM08AndUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        await SeedWorldAsync(fx, seedPostedBill: true);

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261118020321_M08_StockAvailabilityLocationsAndQualityRestrictions");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = """
                SELECT count(*)
                FROM information_schema.tables
                WHERE table_schema = 'ap'
                   OR (table_schema = 'gl' AND table_name = 'receipt_accrual_clearings')
                   OR (table_schema = 'inventory' AND table_name = 'invoice_price_adjustments')
                   OR (table_schema = 'tax' AND table_name = 'tax_determinations');
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
                    (n.nspname = 'ap' AND c.relname IN (
                        'bill_sequences', 'payable_sequences', 'invoice_match_policies', 'supplier_bills',
                        'supplier_bill_lines', 'invoice_match_evaluations', 'supplier_bill_receipt_matches', 'payable_items'))
                    OR (n.nspname = 'gl' AND c.relname = 'receipt_accrual_clearings')
                    OR (n.nspname = 'inventory' AND c.relname = 'invoice_price_adjustments')
                    OR (n.nspname = 'tax' AND c.relname = 'tax_determinations')
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S09_API_01_OperationalIssueThreeWayMatchValuationApTaxesAndSavepointRollback()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx, seedPostedBill: false);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.ProcurementOnlyEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var rcvBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/receiving";
        var invBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/inventory";
        var apBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payables";
        var glBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";

        // 1. Accept 100 PCS @ Rp 50.000 (GRNI Rp 5.000.000), issue 60 PCS -> Dr 5100 / Cr 1300 Rp 3.000.000, leaving 40 PCS (Rp 2.000.000)
        var (poMain, lineMain, accLineMain) = await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemValveId, world.PcsUomId, 100m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-S09-MAIN-01", "idem-s09-main-01");

        var issueRes = await controller.PostAsJsonAsync($"{invBase}/issues", new CreateOperationalIssueRequest(
            world.ItemValveId, null, null, 60m, new DateOnly(2026, 11, 9), "ISS-S09-01", "idem-s09-iss-01", "Issue 60 before bill"));
        Assert.Equal(HttpStatusCode.Created, issueRes.StatusCode);
        var issueDto = (await issueRes.Content.ReadFromJsonAsync<OperationalIssueOutcomeDto>(JsonOptions))!;
        Assert.Equal(60m, issueDto.IssuedBaseQuantity);
        Assert.Equal(3_000_000m, issueDto.IssuedCarryingAmount);
        Assert.Equal(50_000m, issueDto.AppliedUnitCost);
        Assert.NotNull(issueDto.GlJournalId);

        var issJrn = (await (await controller.GetAsync($"{glBase}/journals/{issueDto.GlJournalId!.Value:D}"))
            .Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions))!;
        Assert.Equal(3_000_000m, issJrn.Lines.Single(l => l.AccountCode == "5100").DebitAmount);
        Assert.Equal(3_000_000m, issJrn.Lines.Single(l => l.AccountCode == "1300").CreditAmount);

        // 2. Preview positive variance bill: 100 @ 55.000 + PPN-IN-11 (605.000) + PPH23-SVC-2 (110.000), DeclaredGross = 6.105.000
        var previewRes = await apClerk.PostAsJsonAsync($"{apBase}/bills/preview", new PreviewSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poMain.PurchaseOrderId, "IDR", new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10), 6_105_000m,
            [new BillLineInputRequest(accLineMain.ReceiptAccrualLineId, 100m, 55_000m, world.PpnInputId, world.Pph23Id, "Price +5k")]));
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);
        var preview = (await previewRes.Content.ReadFromJsonAsync<SupplierBillPreviewResponse>(JsonOptions))!;
        Assert.Equal(InvoiceMatchStatuses.ApprovalRequired, preview.OverallMatchStatus);
        Assert.False(preview.IsPostingBlocked);
        Assert.True(preview.RequiresApproval);
        Assert.Equal(500_000m, preview.TotalPriceVarianceAmount);
        Assert.Equal(200_000m, preview.TotalInventoryAdjAmount);
        Assert.Equal(300_000m, preview.TotalPnlVarianceAmount);
        Assert.Equal(605_000m, preview.PpnTaxTotal);
        Assert.Equal(110_000m, preview.WithholdingTaxTotal);
        Assert.Equal(5_995_000m, preview.NetPayableAmount);

        // 3. Create, submit (AP Clerk), four-eyes approve (Controller), and post -> verify GL, pool, accrual, AP, outbox
        var createRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poMain.PurchaseOrderId, "INV-VND-001", "010.001-26.00000001", "BUPOT-001",
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 6_105_000m, "Positive variance bill",
            [new BillLineInputRequest(accLineMain.ReceiptAccrualLineId, 100m, 55_000m, world.PpnInputId, world.Pph23Id, null)]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var draftBill = (await createRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        var submitRes = await apClerk.PostAsJsonAsync($"{apBase}/bills/{draftBill.Bill.SupplierBillId:D}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitRes.StatusCode);

        var approveRes = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{draftBill.Bill.SupplierBillId:D}/decision",
            new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approved price diff +5k", true, false, null));
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // Duplicate vendor invoice check skipped on closed period -> verify closed period 2026-10 blocked first!
        var closedPostRes = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{draftBill.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 10, 25), "idem-post-closed"));
        Assert.Equal(HttpStatusCode.Conflict, closedPostRes.StatusCode);

        var postRes = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{draftBill.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-post-main-01"));
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var postedBill = (await postRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(SupplierBillStatuses.Posted, postedBill.Bill.Status);
        Assert.NotNull(postedBill.Bill.PostedJournalId);
        Assert.NotNull(postedBill.PayableItem);
        Assert.Equal(5_995_000m, postedBill.PayableItem!.NetPayableAmount);
        Assert.Equal(PayableStatuses.Open, postedBill.PayableItem.Status);

        // Verify balanced GL journal lines (2110=5M Dr, 1300=200k Dr, 5200=300k Dr, 1410=605k Dr, 2220=110k Cr, 2100=5.995M Cr)
        var jrn = (await (await controller.GetAsync($"{glBase}/journals/{postedBill.Bill.PostedJournalId!.Value:D}"))
            .Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions))!;
        Assert.Equal(6_105_000m, jrn.TotalDebit);
        Assert.Equal(6_105_000m, jrn.TotalCredit);
        Assert.Equal(5_000_000m, jrn.Lines.Where(l => l.AccountCode == "2110").Sum(l => l.DebitAmount));
        Assert.Equal(200_000m, jrn.Lines.Where(l => l.AccountCode == "1300").Sum(l => l.DebitAmount));
        Assert.Equal(300_000m, jrn.Lines.Where(l => l.AccountCode == "5200").Sum(l => l.DebitAmount));
        Assert.Equal(605_000m, jrn.Lines.Where(l => l.AccountCode == "1410").Sum(l => l.DebitAmount));
        Assert.Equal(110_000m, jrn.Lines.Where(l => l.AccountCode == "2220").Sum(l => l.CreditAmount));
        Assert.Equal(5_995_000m, jrn.Lines.Where(l => l.AccountCode == "2100").Sum(l => l.CreditAmount));

        // Verify inventory pool updated to 40 PCS / Rp 2.200.000 / Rp 55.000 avg, ReceiptAccrual CLEARED, and outbox rows emitted
        var stockDetail = (await (await controller.GetAsync($"{invBase}/items/{world.ItemValveId:D}/detail"))
            .Content.ReadFromJsonAsync<ItemStockDetailResponse>(JsonOptions))!;
        Assert.Equal(40m, stockDetail.Reconciliation.PositionOnHandTotal);
        Assert.Equal(2_200_000m, stockDetail.CarryingValueAmount);
        Assert.Equal(55_000m, stockDetail.MovingAverageUnitCost);

        await using (var dbCheck = fx.CreateMigratorDbContext())
        {
            var accrual = await dbCheck.ReceiptAccruals.AsNoTracking()
                .SingleAsync(a => a.ReceiptAccrualId == accLineMain.ReceiptAccrualId);
            Assert.Equal(ReceiptAccrualStatuses.Cleared, accrual.Status);

            var outboxTypes = await dbCheck.OutboxMessages.AsNoTracking()
                .Where(o => o.TenantId == world.TenantAId && o.LegalEntityId == world.EntityA1Id)
                .Select(o => o.Topic)
                .ToListAsync();
            Assert.Contains("finance.supplierbill.posted.v1", outboxTypes);
            Assert.Contains("inventory.valuation.adjusted.v1", outboxTypes);
        }

        // 4. Negative price variance (55.000 pool baseline after bill 1 -> new PO 100 @ 50.000, issue 60 -> 40 on-hand? Wait: isolate SKU ItemZeroCovId for clean tests)
        // 4a. Zero on-hand coverage (accept 100, issue 100 -> 0 on-hand -> 100% variance to PPV 5200)
        var (poZero, _, accLineZero) = await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemZeroCovId, world.PcsUomId, 100m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-S09-ZERO-01", "idem-s09-zero-01");
        Assert.Equal(HttpStatusCode.Created, (await controller.PostAsJsonAsync($"{invBase}/issues", new CreateOperationalIssueRequest(
            world.ItemZeroCovId, null, null, 100m, new DateOnly(2026, 11, 9), "ISS-ZERO-ALL", "idem-s09-iss-zero", "Issue all 100"))).StatusCode);

        // Partial sequential billing (60 PCS @ 48.000 -> -120k negative variance all to PPV 5200, then 40 PCS @ 50.000 exact, then 3rd bill 1 PCS blocked 409)
        var zeroBill1Res = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poZero.PurchaseOrderId, "INV-ZERO-1", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 3_196_800m, "Partial 60 @ 48k neg variance",
            [new BillLineInputRequest(accLineZero.ReceiptAccrualLineId, 60m, 48_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, zeroBill1Res.StatusCode);
        var zeroBill1 = (await zeroBill1Res.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(-120_000m, zeroBill1.Bill.TotalPriceVarianceAmount);
        Assert.Equal(0m, zeroBill1.Bill.TotalInventoryAdjAmount);
        Assert.Equal(-120_000m, zeroBill1.Bill.TotalPnlVarianceAmount);
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{zeroBill1.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{zeroBill1.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Neg var OK", true, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await controller.PostAsJsonAsync($"{apBase}/bills/{zeroBill1.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-post-zero-1"))).StatusCode);

        // Second partial bill (40 @ 50k exact) submits, approves, and completes clearing
        var zeroBill2Res = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poZero.PurchaseOrderId, "INV-ZERO-2", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 2_220_000m, "Partial 40 @ 50k exact",
            [new BillLineInputRequest(accLineZero.ReceiptAccrualLineId, 40m, 50_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, zeroBill2Res.StatusCode);
        var zeroBill2 = (await zeroBill2Res.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{zeroBill2.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{zeroBill2.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Exact match OK", false, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await controller.PostAsJsonAsync($"{apBase}/bills/{zeroBill2.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-post-zero-2"))).StatusCode);

        // Third bill attempting +1 PCS after full clear -> blocked (409 on post / blocking evaluation)
        var thirdOverPreview = (await (await apClerk.PostAsJsonAsync($"{apBase}/bills/preview", new PreviewSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poZero.PurchaseOrderId, "IDR", new DateOnly(2026, 11, 10), null, 55_500m,
            [new BillLineInputRequest(accLineZero.ReceiptAccrualLineId, 1m, 50_000m, world.PpnInputId, null, null)])))
            .Content.ReadFromJsonAsync<SupplierBillPreviewResponse>(JsonOptions))!;
        Assert.True(thirdOverPreview.IsPostingBlocked);
        Assert.Equal(MatchDimensionOutcomes.Blocked, thirdOverPreview.DqOutcome);

        // 5. Negative inventory price variance + OD-04 floor block + OD-08 NON_PO_EXPENSE + OD-03 public edit blocked + Duplicate supplier invoice + PPh23 missing NPWP + Savepoint rollback
        var (poNeg, _, accLineNeg) = await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemZeroCovId, world.PcsUomId, 100m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-S09-NEG-01", "idem-s09-neg-01");

        // Dilute moving-average pool with a cheaper second receipt (100 @ 10.000 -> pool 200 @ 6.000.000 = 30.000/unit) then issue 100 PCS -> remaining pool 100 @ 3.000.000!
        await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemZeroCovId, world.PcsUomId, 100m, 10_000m,
            new DateOnly(2026, 11, 8), "SJ-S09-CHEAP-01", "idem-s09-cheap-01");
        Assert.Equal(HttpStatusCode.Created, (await controller.PostAsJsonAsync($"{invBase}/issues", new CreateOperationalIssueRequest(
            world.ItemZeroCovId, null, null, 100m, new DateOnly(2026, 11, 9), "ISS-DILUTE-100", "idem-s09-iss-dilute", "Leave 100 PCS @ 3.000.000 carrying"))).StatusCode);

        // Now billing poNeg (GRNI 5.000.000) at 10.000/PCS (1.000.000 -> -4.000.000 inv share against 3.000.000 carrying) breaches OD-04 positive floor!
        var floorBlockedPreview = (await (await apClerk.PostAsJsonAsync($"{apBase}/bills/preview", new PreviewSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poNeg.PurchaseOrderId, "IDR", new DateOnly(2026, 11, 10), null, 1_110_000m,
            [new BillLineInputRequest(accLineNeg.ReceiptAccrualLineId, 100m, 10_000m, world.PpnInputId, null, null)])))
            .Content.ReadFromJsonAsync<SupplierBillPreviewResponse>(JsonOptions))!;
        Assert.True(floorBlockedPreview.IsPostingBlocked);
        Assert.Contains(floorBlockedPreview.BlockingReasons, r => r.Contains("od04_valuation_floor_breached", StringComparison.Ordinal));

        // Duplicate vendor invoice number 'INV-VND-001' fails 409 on post / draft creation
        var dupInvRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poNeg.PurchaseOrderId, "INV-VND-001", null, null,
            new DateOnly(2026, 11, 10), null, "IDR", 5_550_000m, "Dup invoice",
            [new BillLineInputRequest(accLineNeg.ReceiptAccrualLineId, 100m, 50_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Conflict, dupInvRes.StatusCode);

        // OD-08: NON_PO_EXPENSE blocked (409)
        var nonPoRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.NonPoExpense, poNeg.PurchaseOrderId, "INV-NON-PO", null, null,
            new DateOnly(2026, 11, 10), null, "IDR", 100_000m, null,
            [new BillLineInputRequest(accLineNeg.ReceiptAccrualLineId, 1m, 50_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Conflict, nonPoRes.StatusCode);

        // OD-03: public non-zero tolerance policy edit blocked (409)
        var od03Res = await controller.PutAsJsonAsync($"{apBase}/match-policy", new UpdateInvoiceMatchPolicyRequest(1m, 0m, 0m, 0m, 0m, null));
        Assert.Equal(HttpStatusCode.Conflict, od03Res.StatusCode);

        // OD-07: PPH23 without supplier NPWP blocked
        var (poNoNpwp, _, accNoNpwp) = await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierNoNpwpId, world.ItemZeroCovId, world.PcsUomId, 10m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-S09-NONPWP", "idem-s09-nonpwp");
        var noNpwpPreview = (await (await apClerk.PostAsJsonAsync($"{apBase}/bills/preview", new PreviewSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poNoNpwp.PurchaseOrderId, "IDR", new DateOnly(2026, 11, 10), null, 545_000m,
            [new BillLineInputRequest(accNoNpwp.ReceiptAccrualLineId, 10m, 50_000m, world.PpnInputId, world.Pph23Id, null)])))
            .Content.ReadFromJsonAsync<SupplierBillPreviewResponse>(JsonOptions))!;
        Assert.True(noNpwpPreview.IsPostingBlocked);
        Assert.Contains(noNpwpPreview.BlockingReasons, r => r.Contains("npwp", StringComparison.OrdinalIgnoreCase));

        // 6. Savepoint fault injection rollback: create exact match bill on poNeg, inject fault inside PostBillAsync -> bill stays DRAFT & accrual stays OPEN
        var spBillRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poNeg.PurchaseOrderId, "INV-SAVEPOINT-01", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 5_328_000m, "Negative -200k variance test + savepoint",
            [new BillLineInputRequest(accLineNeg.ReceiptAccrualLineId, 100m, 48_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, spBillRes.StatusCode);
        var spBill = (await spBillRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(-200_000m, spBill.Bill.TotalInventoryAdjAmount);

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{spBill.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{spBill.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approved for savepoint fault check", true, false, null))).StatusCode);

        await using (var dbFault = fx.CreateMigratorDbContext())
        await using (var faultTx = await dbFault.Database.BeginTransactionAsync())
        {
            var faultRes = await SupplierBillCoordinator.PostBillAsync(
                dbFault,
                new PostSupplierBillCommand(
                    world.TenantAId,
                    world.EntityA1Id,
                    spBill.Bill.SupplierBillId,
                    "IDR",
                    new DateOnly(2026, 11, 10),
                    "idem-fault-savepoint",
                    world.ControllerId,
                    DateTimeOffset.UtcNow,
                    FaultAfterSubledgerBeforeCommit: () => throw new InvalidOperationException("simulated_fault_after_subledger")),
                CancellationToken.None);
            Assert.False(faultRes.Succeeded);
            Assert.Contains("simulated_fault_after_subledger", faultRes.ErrorDetail ?? faultRes.ErrorCode ?? string.Empty, StringComparison.Ordinal);
            await faultTx.CommitAsync();
        }

        // Wait: if PostBillAsync caught InvalidOperationException and rolled back to savepoint, commit or check DB directly! Let's assert bill is still APPROVED & unposted!
        var spAfterCheck = (await (await controller.GetAsync($"{apBase}/bills/{spBill.Bill.SupplierBillId:D}"))
            .Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(SupplierBillStatuses.Approved, spAfterCheck.Bill.Status);
        Assert.Null(spAfterCheck.Bill.PostedJournalId);

        // Finally verify REVIEWED_INTERNAL policy tolerance boundary via DB-seeded policy (within PP + BD write-off without price-variance acknowledgement)
        await using (var dbTol = fx.CreateMigratorDbContext())
        {
            var oldPolId = await dbTol.InvoiceMatchPolicies
                .Where(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.IsDefault)
                .Select(p => p.PolicyId)
                .SingleAsync();
            await dbTol.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ap.invoice_match_policies SET is_default = FALSE WHERE policy_id = {oldPolId};");
            dbTol.InvoiceMatchPolicies.Add(InvoiceMatchPolicy.CreateReviewedForVerification(
                Guid.NewGuid(), world.TenantAId, world.EntityA1Id, "REV_TOL_02", "Reviewed 2% PP / Rp 1.000 BD",
                2.0m, 250_000m, 1_000m, world.WriteOffAccountId, isDefault: true, DateTimeOffset.UtcNow));
            await dbTol.SaveChangesAsync();
        }

        // Submit + approve exact/within-tolerance bill on poNoNpwp (10 @ 50.500 = 505.000, PPN 55.550 -> computed 560.550, declared 560.750 -> BD +200 <= 1.000, PP +1% <= 2%)
        var tolRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, poNoNpwp.PurchaseOrderId, "INV-TOL-01", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 560_750m, "Within reviewed PP+BD tolerance",
            [new BillLineInputRequest(accNoNpwp.ReceiptAccrualLineId, 10m, 50_500m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, tolRes.StatusCode);
        var tolBill = (await tolRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(InvoiceMatchStatuses.WithinTolerance, tolBill.Bill.MatchStatus);

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{tolBill.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{tolBill.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Within tolerance OK", false, false, null))).StatusCode);
        var tolPost = await controller.PostAsJsonAsync($"{apBase}/bills/{tolBill.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-post-tol"));
        Assert.Equal(HttpStatusCode.Created, tolPost.StatusCode);
    }

    [Fact]
    public async Task S09_API_02_ConcurrentBillPostRaceAndIdempotencyProtectGrniAndAp()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx, seedPostedBill: false);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.ProcurementOnlyEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var c1 = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);
        using var c2 = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var apBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payables";
        var (po, _, accLine) = await AcceptPoStockAsync(
            buyer, c1, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemValveId, world.PcsUomId, 100m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-RACE-01", "idem-s09-race-rcv");

        // Create TWO exact-match bills for 60 PCS each against the SAME 100 PCS open GRNI line and approve both before concurrent post
        var bill1Res = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "INV-RACE-01", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 3_330_000m, "Race 1",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 60m, 50_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, bill1Res.StatusCode);
        var bill1 = (await bill1Res.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{bill1.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c1.PostAsJsonAsync($"{apBase}/bills/{bill1.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Race 1 OK", false, false, null))).StatusCode);

        var bill2Res = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "INV-RACE-02", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 3_330_000m, "Race 2",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 60m, 50_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, bill2Res.StatusCode);
        var bill2 = (await bill2Res.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{bill2.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c2.PostAsJsonAsync($"{apBase}/bills/{bill2.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Race 2 OK", false, false, null))).StatusCode);

        var t1 = c1.PostAsJsonAsync($"{apBase}/bills/{bill1.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-race-p1"));
        var t2 = c2.PostAsJsonAsync($"{apBase}/bills/{bill2.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-race-p2"));
        var results = await Task.WhenAll(t1, t2);

        var created = results.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        var conflicts = results.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Single(created);
        Assert.Single(conflicts);

        var winnerDto = (await created[0].Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        var winnerIdemp = ReferenceEquals(created[0], results[0]) ? "idem-race-p1" : "idem-race-p2";

        // Idempotent replay with same bill & key returns 200 OK with same GlJournalId and zero duplicate journals
        var replayRes = await c1.PostAsJsonAsync(
            $"{apBase}/bills/{winnerDto.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 11, 10), winnerIdemp));
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);
        var replayDto = (await replayRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.True(replayDto.WasIdempotentReplay);
        Assert.Equal(winnerDto.Bill.PostedJournalId, replayDto.Bill.PostedJournalId);

        // Verify remaining unbilled GRNI quantity is exactly 40 (never negative or >100 billed)
        await using var dbCheck = fx.CreateMigratorDbContext();
        var accLineFinal = await dbCheck.ReceiptAccrualLines.AsNoTracking().SingleAsync(l => l.ReceiptAccrualLineId == accLine.ReceiptAccrualLineId);
        Assert.Equal(60m, accLineFinal.BilledBaseQuantity);
        Assert.Equal(40m, accLineFinal.RemainingUnbilledBaseQuantity);
    }

    [Fact]
    public async Task S09_API_03_PermissionSegregationSoDHostileCrossScopeAndPaymentBlockRelease()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx, seedPostedBill: false);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.ProcurementOnlyEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);
        using var foreign = await CreateSignedInClientAsync(factory, world.ForeignUserEmail, world.Password);

        var apBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payables";
        var sbyApBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/payables";

        var (po, _, accLine) = await AcceptPoStockAsync(
            buyer, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemValveId, world.PcsUomId, 50m, 50_000m,
            new DateOnly(2026, 11, 8), "SJ-SOD-01", "idem-s09-sod-rcv");

        // 1. ProcurementOnly (Rina) has zero AP permissions -> 403 on overview and mutations
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync($"{apBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync($"{apBase}/bills/preview", new PreviewSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "IDR", new DateOnly(2026, 11, 10), null, 2_775_000m,
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 50m, 50_000m, world.PpnInputId, null, null)]))).StatusCode);

        // 2. OD-02 self-approval blocked: Controller creates + submits price variance bill, then tries to self-approve -> 409
        var selfBillRes = await controller.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "INV-SELF-01", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 2_886_000m, "Self submit check",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 50m, 52_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, selfBillRes.StatusCode);
        var selfBill = (await selfBillRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{selfBill.Bill.SupplierBillId:D}/submit", new { })).StatusCode);

        var selfApprove = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{selfBill.Bill.SupplierBillId:D}/decision",
            new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Attempt self-approve", true, false, null));
        Assert.Equal(HttpStatusCode.Conflict, selfApprove.StatusCode);

        // AP_CLERK cannot approve or post -> 403
        Assert.Equal(HttpStatusCode.Forbidden, (await apClerk.PostAsJsonAsync(
            $"{apBase}/bills/{selfBill.Bill.SupplierBillId:D}/decision",
            new DecideSupplierBillRequest(ApprovalDecisions.Approved, "No approve perm", true, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await apClerk.PostAsJsonAsync(
            $"{apBase}/bills/{selfBill.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-clerk-post-deny"))).StatusCode);

        // Read-only Entity A2 (NUSA-SBY): Controller can GET overview (ap.read) -> 200, but mutating POST/PUT -> 403 Forbidden
        Assert.Equal(HttpStatusCode.OK, (await controller.GetAsync($"{sbyApBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PostAsJsonAsync($"{sbyApBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "INV-SBY-RO", null, null,
            new DateOnly(2026, 11, 10), null, "IDR", 100_000m, null,
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 1m, 50_000m, world.PpnInputId, null, null)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PutAsJsonAsync($"{sbyApBase}/match-policy", new UpdateInvoiceMatchPolicyRequest(0m, 0m, 0m, 0m, 0m, null))).StatusCode);

        // Cross-tenant hostile selector: Foreign user (Tenant B) probing Tenant A bill or overview -> 403/404
        var crossRes = await foreign.GetAsync($"{apBase}/bills/{selfBill.Bill.SupplierBillId:D}");
        Assert.True(crossRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);

        // Reject self-submitted variance bill -> resubmit by AP Clerk -> approve with ApplyPaymentBlock=true by Controller -> post -> PAYMENT_BLOCKED -> RELEASED!
        // Since Controller can't decide on own submission, reject using AP Clerk is denied, so let AP Clerk create fresh bill and test PAYMENT_BLOCKED -> RELEASED cleanly
        var blkBillRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, po.PurchaseOrderId, "INV-BLK-01", null, null,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 2_886_000m, "Hold payment",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 50m, 52_000m, world.PpnInputId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, blkBillRes.StatusCode);
        var blkBill = (await blkBillRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{blkBill.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{apBase}/bills/{blkBill.Bill.SupplierBillId:D}/decision",
            new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approve variance with payment hold", true, true, "Awaiting physical Faktur Pajak"))).StatusCode);

        var postBlk = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{blkBill.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 11, 10), "idem-post-blk"));
        Assert.Equal(HttpStatusCode.Created, postBlk.StatusCode);
        var postedBlk = (await postBlk.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(PaymentBlockStatuses.PaymentBlocked, postedBlk.Bill.PaymentBlockStatus);
        Assert.Equal(PaymentBlockStatuses.PaymentBlocked, postedBlk.PayableItem!.PaymentBlockStatus);

        // AP_CLERK cannot release payment block (403); Controller (ap.approve) CAN release PAYMENT_BLOCKED -> RELEASED
        Assert.Equal(HttpStatusCode.Forbidden, (await apClerk.PostAsJsonAsync(
            $"{apBase}/bills/{blkBill.Bill.SupplierBillId:D}/payment-block",
            new UpdateBillPaymentBlockRequest(PaymentBlockStatuses.Released, "Unprivileged clerk release attempt"))).StatusCode);

        var releaseRes = await controller.PostAsJsonAsync(
            $"{apBase}/bills/{blkBill.Bill.SupplierBillId:D}/payment-block",
            new UpdateBillPaymentBlockRequest(PaymentBlockStatuses.Released, "Original Faktur Pajak verified"));
        Assert.Equal(HttpStatusCode.OK, releaseRes.StatusCode);
        var released = (await releaseRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(PaymentBlockStatuses.Released, released.Bill.PaymentBlockStatus);
        Assert.Equal(PaymentBlockStatuses.Released, released.PayableItem!.PaymentBlockStatus);
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

    private static async Task<(PurchaseOrderDetailDto Po, PurchaseOrderLineDto PoLine, EligibleAccrualLineDto AccrualLine)> AcceptPoStockAsync(
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
        var procBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/procurement";
        var rcvBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/receiving";
        var apBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/payables";

        var createRes = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            supplierId, "IDR", date, date.AddDays(14), "Gudang Jakarta", 30, null, null,
            [new PoLineInputDto("L001", itemId, uomId, qty, unitPrice, null)]));
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
            [new ArrivalLineRequest(poLine.PoLineId, uomId, qty, false, DiscrepancyReasonCodes.None, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = (await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest($"{idempBase}-dc", 0m, 0m, qty, 0m, DiscrepancyReasonCodes.None, "Released", null))).StatusCode);

        var accRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept", new AcceptReceiptRequest(
            $"{idempBase}-acc", date, StockStatuses.Unrestricted, "Gate 2 acceptance",
            [new AcceptReceiptLineRequest(grn.Lines[0].GoodsReceiptLineId, qty)]));
        Assert.Equal(HttpStatusCode.Created, accRes.StatusCode);

        var overview = (await (await controller.GetAsync($"{apBase}/overview"))
            .Content.ReadFromJsonAsync<PayablesOverviewResponse>(JsonOptions))!;
        var accLine = overview.EligibleAccrualLines.Single(l => l.PoLineId == poLine.PoLineId);
        return (approved, poLine, accLine);
    }

    private static async Task<S09SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx, bool seedPostedBill)
    {
        const string pw = "Pass!word1234";
        var (apClerkId, apClerkEmail, _) = await fx.CreateTestUserAsync("siti.s09@test.local", "Siti S09 AP Clerk", pw);
        var (controllerId, controllerEmail, _) = await fx.CreateTestUserAsync("budi.s09@test.local", "Budi S09 Controller", pw);
        var (procOnlyId, procOnlyEmail, _) = await fx.CreateTestUserAsync("rina.s09@test.local", "Rina S09 Buyer", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.s09@test.local", "Samudra Staff", pw);

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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, apClerkId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, controllerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, procOnlyId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var perm in RoleTemplates.GetPermissions(RoleTemplates.ApClerk))
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, apClerkId, perm, now, null, now));
        }

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.CatalogManage,
            PermissionCodes.PartnersRead, PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove,
            PermissionCodes.ReceivingRead, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage,
            PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost,
            PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost,
            PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, controllerId, perm, now, null, now));
        }

        // Controller has read-only AP on NUSA-SBY (EntityA2)
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.WorkspaceView, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.ApRead, now, null, now));

        // ProcurementOnly (Rina): Procurement + Receiving + Inventory, ZERO AP/Finance permissions
        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept,
            PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, procOnlyId, perm, now, null, now));
        }

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ApRead, PermissionCodes.ApManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, perm, now, null, now));
        }

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.SupplierBillApprovalPolicyCode, "Bill Four-Eyes Approval", true, false, now));
        db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));
        db.InvoiceMatchPolicies.Add(InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03 Safe", true, now));

        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1410", "PPN Masukan", GlAccountTypes.Asset, GlAccountCategories.TaxReceivable, null, true, true, now);
        var apAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2100", "Hutang Usaha", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, null, true, true, now);
        var grniAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        var pphAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2220", "Hutang PPh 23", GlAccountTypes.Liability, GlAccountCategories.WithholdingTaxPayable, null, true, true, now);
        var cogsAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "Harga Pokok Penjualan", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, true, now);
        var ppvAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5200", "Selisih Harga Pembelian", GlAccountTypes.Expense, GlAccountCategories.PurchasePriceVariance, null, true, true, now);
        var bdAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5290", "Pembulatan Faktur Pemasok", GlAccountTypes.Expense, GlAccountCategories.OperatingExpense, null, true, true, now);
        db.GlAccounts.AddRange(invAcc, ppnAcc, apAcc, grniAcc, pphAcc, cogsAcc, ppvAcc, bdAcc);

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
        var supNoNpwp = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-NONPWP", "CV Tanpa NPWP", "Tanpa NPWP", true, false, null, TaxPkpStatuses.NonPkp, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(supPkp, supNoNpwp);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);

        var itemValve = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S09-VALVE-01", "Katup Industri PN16 50mm", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        var itemZeroCov = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S09-ZERO-01", "Seal Gasket Industri", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Items.AddRange(itemValve, itemZeroCov);
        await db.SaveChangesAsync();

        Guid seedBillId = Guid.Empty, seedBillLineId = Guid.Empty, seedPayableId = Guid.Empty, seedEvalId = Guid.Empty, seedMatchId = Guid.Empty, seedClearId = Guid.Empty, seedAdjId = Guid.Empty, seedTaxDetId = Guid.Empty;
        if (seedPostedBill)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);

            var poRes = await PurchaseOrderWorkflowService.CreateDraftAsync(db, new CreatePoDraftCommand(tenantAId, entityA1Id, "IDR", "IDR", supPkp.PartnerId, new DateOnly(2026, 11, 5), null, "Gudang", 30, null, null,
                [new PoLineInput("L001", itemValve.ItemId, pcs.UomId, 100m, 50_000m, ppnCode.TaxCodeId)], procOnlyId, now), CancellationToken.None);
            await PurchaseOrderWorkflowService.SubmitRevisionAsync(db, new SubmitPoRevisionCommand(tenantAId, entityA1Id, poRes.PurchaseOrder!.PurchaseOrderId, poRes.Revision!.RevisionId, null, procOnlyId, now), CancellationToken.None);
            await PurchaseOrderWorkflowService.DecideRevisionAsync(db, new DecidePoRevisionCommand(tenantAId, entityA1Id, poRes.PurchaseOrder.PurchaseOrderId, poRes.Revision.RevisionId, ApprovalDecisions.Approved, "Seed PO", controllerId, now), CancellationToken.None);
            var poLine = await db.PurchaseOrderLines.SingleAsync(l => l.RevisionId == poRes.Revision.RevisionId);
            var gr = await GoodsReceiptWorkflowService.RecordArrivalAsync(db, new RecordGoodsReceiptCommand(tenantAId, entityA1Id, poRes.PurchaseOrder.PurchaseOrderId, "SJ-SEED-S09", "idem-seed-gr", new DateOnly(2026, 11, 5), "Dock", null, null, null,
                [new ArrivalLineInput(poLine.PoLineId, pcs.UomId, 100m, false, DiscrepancyReasonCodes.None, null, null)], procOnlyId, now), CancellationToken.None);
            var grLine = await db.GoodsReceiptLines.SingleAsync(l => l.GoodsReceiptId == gr.Receipt!.GoodsReceiptId);
            await GoodsReceiptWorkflowService.RecordDockCheckAsync(db, new RecordDockCheckCommand(tenantAId, entityA1Id, gr.Receipt!.GoodsReceiptId, grLine.GoodsReceiptLineId, "idem-seed-dc", 0m, 0m, 100m, 0m, DiscrepancyReasonCodes.None, "OK", null, procOnlyId, now), CancellationToken.None);
            var acc = await ReceiptAcceptanceCoordinator.AcceptReceiptAsync(db, new AcceptReceiptCommand(tenantAId, entityA1Id, "IDR", gr.Receipt.GoodsReceiptId, "idem-seed-acc", new DateOnly(2026, 11, 6), StockStatuses.Unrestricted, "Seed accept",
                [new AcceptReceiptLineInput(grLine.GoodsReceiptLineId, 100m)], controllerId, now), CancellationToken.None);
            Assert.True(acc.Succeeded);

            await InventoryValuationService.PostOperationalIssueAsync(db, new PostOperationalIssueCommand(
                tenantAId, entityA1Id, "IDR", itemValve.ItemId, null, null, 60m, new DateOnly(2026, 11, 7), "ISS-SEED-01", "idem-seed-iss", null, controllerId, now), CancellationToken.None);

            var accLineId = await db.ReceiptAccrualLines.Where(l => l.TenantId == tenantAId && l.PoLineId == poLine.PoLineId).Select(l => l.ReceiptAccrualLineId).SingleAsync();
            var draftBill = await SupplierBillCoordinator.CreateDraftAsync(db, new CreateSupplierBillCommand(
                tenantAId, entityA1Id, "IDR", SupplierBillTypes.PoThreeWayMatch, poRes.PurchaseOrder.PurchaseOrderId,
                "INV-SEED-01", "010.000-26.0000001", null, new DateOnly(2026, 11, 8), new DateOnly(2026, 12, 8), "IDR",
                6_105_000m, "Seeded bill", [new BillLineDraftInput(accLineId, 100m, 55_000m, ppnCode.TaxCodeId, pph23Code.TaxCodeId)], apClerkId, now), CancellationToken.None);
            Assert.True(draftBill.Succeeded);
            await SupplierBillCoordinator.SubmitBillAsync(db, new SubmitSupplierBillCommand(tenantAId, entityA1Id, draftBill.Bill!.SupplierBillId, "IDR", apClerkId, now), CancellationToken.None);
            await SupplierBillCoordinator.DecideBillAsync(db, new DecideSupplierBillCommand(tenantAId, entityA1Id, draftBill.Bill.SupplierBillId, ApprovalDecisions.Approved, "Seed approve", true, false, null, controllerId, now), CancellationToken.None);
            var postOut = await SupplierBillCoordinator.PostBillAsync(db, new PostSupplierBillCommand(tenantAId, entityA1Id, draftBill.Bill.SupplierBillId, "IDR", new DateOnly(2026, 11, 8), "idem-seed-post", controllerId, now), CancellationToken.None);
            Assert.True(postOut.Succeeded, $"{postOut.ErrorCode}: {postOut.ErrorDetail}");

            seedBillId = postOut.Bill!.SupplierBillId;
            seedBillLineId = postOut.Lines[0].SupplierBillLineId;
            seedPayableId = postOut.PayableItem!.PayableItemId;
            seedEvalId = postOut.Evaluation!.EvaluationId;
            seedMatchId = postOut.ReceiptMatches[0].MatchId;
            seedAdjId = postOut.PriceAdjustments[0].AdjustmentId;
            seedTaxDetId = postOut.TaxDeterminations[0].DeterminationId;
            seedClearId = await db.ReceiptAccrualClearings.Where(c => c.TenantId == tenantAId).Select(c => c.ClearingId).FirstAsync();

            await tx.CommitAsync();
        }

        return new S09SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            apClerkId, apClerkEmail, controllerId, controllerEmail, procOnlyId, procOnlyEmail, foreignId, foreignEmail,
            supPkp.PartnerId, supNoNpwp.PartnerId, pcs.UomId, itemValve.ItemId, itemZeroCov.ItemId,
            ppnCode.TaxCodeId, pph23Code.TaxCodeId, bdAcc.AccountId,
            seedBillId, seedBillLineId, seedPayableId, seedEvalId, seedMatchId, seedClearId, seedAdjId, seedTaxDetId, pw);
    }
}
