using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;
using Xunit;

namespace Orphac.Athera.IntegrationTests;

public sealed class S18ReleaseCandidateFlowPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record RcFlowWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid BuyerId,
        string BuyerEmail,
        Guid WarehouseId,
        string WarehouseEmail,
        Guid ApClerkId,
        string ApClerkEmail,
        Guid ControllerId,
        string ControllerEmail,
        Guid SalesId,
        string SalesEmail,
        Guid SupplierId,
        Guid CustomerId,
        Guid PcsUomId,
        Guid ItemId,
        Guid LocMainId,
        Guid BankAccountId,
        Guid PpnIn0Id,
        Guid PpnOut0Id,
        Guid PeriodNovId,
        Guid CashAccountId,
        Guid EquityAccountId,
        string Password);

    [Fact]
    public async Task S18_RC_01_FullP2pAndO2cNumericStoryReconciliationsAndPeriodCloseBoundary()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedRcFlowWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await SignInAndGetCsrfAsync(factory, world.BuyerEmail, world.Password);
        using var warehouse = await SignInAndGetCsrfAsync(factory, world.WarehouseEmail, world.Password);
        using var apClerk = await SignInAndGetCsrfAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await SignInAndGetCsrfAsync(factory, world.ControllerEmail, world.Password);
        using var sales = await SignInAndGetCsrfAsync(factory, world.SalesEmail, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";

        async Task<AccountingReportsOverviewResponse> Overview() =>
            (await controller.GetFromJsonAsync<AccountingReportsOverviewResponse>(
                $"{baseUrl}/reports/overview?periodId={world.PeriodNovId:D}", JsonOptions))!;

        static void AssertBalance(TrialBalanceReportDto tb, string accountCode, decimal expectedDebit, decimal expectedCredit)
        {
            var row = tb.Rows.SingleOrDefault(r => r.AccountCode == accountCode);
            var debit = row?.EndingDebit ?? 0m;
            var credit = row?.EndingCredit ?? 0m;
            Assert.True(
                debit == expectedDebit && credit == expectedCredit,
                $"Account {accountCode}: expected Dr {expectedDebit} / Cr {expectedCredit}, got Dr {debit} / Cr {credit}");
        }

        async Task<long> JournalCountAsync()
        {
            await using var db = fx.CreateMigratorDbContext();
            return await db.JournalEntries.LongCountAsync(
                j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id);
        }

        async Task<decimal> OnHandAsync()
        {
            await using var db = fx.CreateMigratorDbContext();
            var positions = await db.StockPositions
                .Where(p => p.TenantId == world.TenantAId && p.LegalEntityId == world.EntityA1Id && p.ItemId == world.ItemId)
                .ToListAsync();
            return positions.Sum(p =>
                p.UnrestrictedBaseQuantity + p.QualityInspectionBaseQuantity + p.BlockedBaseQuantity);
        }

        // 0. Opening capital journal via API: Dr 1110 5,000,000 / Cr 3100 5,000,000
        var capRes = await controller.PostAsJsonAsync($"{baseUrl}/finance/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 2), "IDR", "Setoran modal awal RC",
            "FINANCE", "MANUAL_JOURNAL", "RC-CAP-01", "rc-cap-1",
            [
                new JournalLineInputDto(world.CashAccountId, "Kas modal awal", 5_000_000m, 0m, null, null, null),
                new JournalLineInputDto(world.EquityAccountId, "Modal disetor", 0m, 5_000_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.Created, capRes.StatusCode);
        Assert.Equal(1, await JournalCountAsync());

        // 1. PO 100 PCS @ 50,000; buyer submits, buyer self-approve rejected, controller approves
        var poRes = await buyer.PostAsJsonAsync($"{baseUrl}/procurement/purchase-orders", new CreatePurchaseOrderRequest(
            world.SupplierId, "IDR", new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 17),
            "Gudang RC Jakarta", 30, null, null,
            [new PoLineInputDto("L001", world.ItemId, world.PcsUomId, 100m, 50_000m, null)]));
        Assert.Equal(HttpStatusCode.Created, poRes.StatusCode);
        var po = (await poRes.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{baseUrl}/procurement/purchase-orders/{po.PurchaseOrderId:D}/revisions/{po.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null))).StatusCode);

        var selfApprove = await buyer.PostAsJsonAsync(
            $"{baseUrl}/procurement/purchase-orders/{po.PurchaseOrderId:D}/revisions/{po.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest(ApprovalDecisions.Approved, "Self approve"));
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);

        var appPo = await controller.PostAsJsonAsync(
            $"{baseUrl}/procurement/purchase-orders/{po.PurchaseOrderId:D}/revisions/{po.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest(ApprovalDecisions.Approved, "Approved by controller"));
        Assert.Equal(HttpStatusCode.OK, appPo.StatusCode);
        var approvedPo = (await appPo.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;
        var poLine = approvedPo.Revisions[0].Lines[0];

        // 2. Dock arrival of 60 PCS; dock check releases 55, rejects 5 -> Gate 1 unvalued (no journal, no stock)
        var journalsBeforeArrival = await JournalCountAsync();
        var arrRes = await buyer.PostAsJsonAsync($"{baseUrl}/receiving/receipts", new RecordGoodsReceiptRequest(
            approvedPo.PurchaseOrderId, "SJ-RC01-01", "rc01-gr", new DateOnly(2026, 11, 10),
            "Dermaga RC", null, null, null,
            [new ArrivalLineRequest(poLine.PoLineId, world.PcsUomId, 60m, false, null, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = (await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;

        var dockRes = await buyer.PostAsJsonAsync(
            $"{baseUrl}/receiving/receipts/{grn.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest("rc01-dc", 0m, 0m, 55m, 5m,
                DiscrepancyReasonCodes.DamagedPackaging, "5 PCS rusak, 55 PCS diteruskan", null));
        Assert.Equal(HttpStatusCode.OK, dockRes.StatusCode);
        Assert.Equal(journalsBeforeArrival, await JournalCountAsync());
        Assert.Equal(0m, await OnHandAsync());

        // 3. Valued acceptance of 55 -> Dr 1300 2,750,000 / Cr 2110 2,750,000; idempotent replay is a no-op
        var accReq = new AcceptReceiptRequest("rc01-acc", new DateOnly(2026, 11, 10),
            StockStatuses.Unrestricted, "Accept 55 PCS",
            [new AcceptReceiptLineRequest(grn.Lines[0].GoodsReceiptLineId, 55m)]);
        var accRes = await buyer.PostAsJsonAsync($"{baseUrl}/receiving/receipts/{grn.GoodsReceiptId:D}/accept", accReq);
        Assert.Equal(HttpStatusCode.Created, accRes.StatusCode);

        var ovAfterAccept = await Overview();
        AssertBalance(ovAfterAccept.TrialBalance, "1300", 2_750_000m, 0m);
        AssertBalance(ovAfterAccept.TrialBalance, "2110", 0m, 2_750_000m);
        Assert.Equal(55m, await OnHandAsync());

        var journalsBeforeReplay = await JournalCountAsync();
        var accReplay = await buyer.PostAsJsonAsync($"{baseUrl}/receiving/receipts/{grn.GoodsReceiptId:D}/accept", accReq);
        Assert.Equal(HttpStatusCode.OK, accReplay.StatusCode);
        var accReplayDto = (await accReplay.Content.ReadFromJsonAsync<ValuedAcceptanceDetailDto>(JsonOptions))!;
        Assert.True(accReplayDto.WasIdempotentReplay);
        Assert.Equal(journalsBeforeReplay, await JournalCountAsync());
        Assert.Equal(55m, await OnHandAsync());
        AssertBalance((await Overview()).TrialBalance, "1300", 2_750_000m, 0m);

        // 4. Supplier bill 55 @ 50,000 (gross 2,750,000, no tax): clerk drafts+submits, controller approves+posts
        var apOv = (await (await controller.GetAsync($"{baseUrl}/payables/overview"))
            .Content.ReadFromJsonAsync<PayablesOverviewResponse>(JsonOptions))!;
        var accLine = apOv.EligibleAccrualLines.Single(l => l.PoLineId == poLine.PoLineId);

        var billRes = await apClerk.PostAsJsonAsync($"{baseUrl}/payables/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, approvedPo.PurchaseOrderId, "INV-RC01-01", null, null,
            new DateOnly(2026, 11, 12), new DateOnly(2026, 12, 12), "IDR", 2_750_000m, "RC01 bill",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, 55m, 50_000m, world.PpnIn0Id, null, null)]));
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);
        var bill = (await billRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync(
            $"{baseUrl}/payables/bills/{bill.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{baseUrl}/payables/bills/{bill.Bill.SupplierBillId:D}/decision",
            new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approved", false, false, null))).StatusCode);
        var postBillRes = await controller.PostAsJsonAsync(
            $"{baseUrl}/payables/bills/{bill.Bill.SupplierBillId:D}/post",
            new PostSupplierBillRequest(new DateOnly(2026, 11, 12), "rc01-bill-post"));
        Assert.Equal(HttpStatusCode.Created, postBillRes.StatusCode);
        var postedBill = (await postBillRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        var payableId = postedBill.PayableItem!.PayableItemId;

        var ovAfterBill = await Overview();
        AssertBalance(ovAfterBill.TrialBalance, "2110", 0m, 0m);
        AssertBalance(ovAfterBill.TrialBalance, "2100", 0m, 2_750_000m);

        // 5. Payment proposal allocating 1,000,000; simulate file = no GL; clerk submits; controller releases
        var propRes = await apClerk.PostAsJsonAsync($"{baseUrl}/payments", new CreateSupplierPaymentProposalRequest(
            world.BankAccountId, world.SupplierId, "PT Bank Mandiri", "122-000-4455", "PT Pemasok RC",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 13), "RC01 partial payment",
            [new PaymentAllocationInputRequest(payableId, 1_000_000m, "Partial 1")]));
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var payment = (await propRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;

        var journalsBeforeSim = await JournalCountAsync();
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync(
            $"{baseUrl}/payments/{payment.Payment.SupplierPaymentId:D}/simulate-file",
            new SimulatePaymentInstructionRequest(null))).StatusCode);
        Assert.Equal(journalsBeforeSim, await JournalCountAsync());
        var payablesAfterSim = (await controller.GetFromJsonAsync<PayablesOverviewResponse>(
            $"{baseUrl}/payables/overview", JsonOptions))!;
        Assert.Equal(2_750_000m, payablesAfterSim.Payables.Single(p => p.PayableItemId == payableId).RemainingOpenAmount);

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync(
            $"{baseUrl}/payments/{payment.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);
        var relRes = await controller.PostAsJsonAsync(
            $"{baseUrl}/payments/{payment.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 13), "REL-RC01-01", "rc01-rel-1"));
        Assert.Equal(HttpStatusCode.OK, relRes.StatusCode);

        var ovAfterRelease = await Overview();
        AssertBalance(ovAfterRelease.TrialBalance, "2100", 0m, 1_750_000m);
        AssertBalance(ovAfterRelease.TrialBalance, "1120", 0m, 1_000_000m);

        // 6. Import DEBIT bank line 1,000,000 and reconcile to the payment -> Dr 1120 / Cr 1110
        var stmtRes = await controller.PostAsJsonAsync($"{baseUrl}/payments/bank-statements", new ImportBankStatementRequest(
            world.BankAccountId, "STMT-RC01-OUT", new DateOnly(2026, 11, 14), 5_000_000m, 4_000_000m,
            "2026-11-14,DEBIT,1000000,TRF-RC01-OUT,PT Pemasok RC,Pembayaran INV-RC01-01", null));
        Assert.Equal(HttpStatusCode.Created, stmtRes.StatusCode);
        var payOv = (await controller.GetFromJsonAsync<PaymentsOverviewResponse>(
            $"{baseUrl}/payments/overview", JsonOptions))!;
        var outLine = payOv.StatementLines.Single(l => l.BankReference == "TRF-RC01-OUT");

        var recRes = await controller.PostAsJsonAsync($"{baseUrl}/payments/reconcile", new ReconcileBankStatementLineRequest(
            outLine.BankStatementLineId, payment.Payment.SupplierPaymentId, new DateOnly(2026, 11, 14), 1_000_000m, "rc01-rec-1"));
        Assert.Equal(HttpStatusCode.OK, recRes.StatusCode);

        var ovAfterRec = await Overview();
        AssertBalance(ovAfterRec.TrialBalance, "1120", 0m, 0m);
        AssertBalance(ovAfterRec.TrialBalance, "1110", 4_000_000m, 0m);

        // 7. SO 10 PCS @ 80,000: sales submits, controller approves; warehouse reserves 10, picks, dispatches 10
        var soRes = await sales.PostAsJsonAsync($"{baseUrl}/sales-orders", new CreateSalesOrderRequest(
            world.CustomerId, "IDR", new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 25),
            "Alamat Pelanggan RC", 14, "PO-CUST-RC01", false, "RC01 sales order",
            [new SoLineInputDto(null, world.ItemId, world.PcsUomId, 10m, 80_000m, 0m, null)]));
        Assert.Equal(HttpStatusCode.Created, soRes.StatusCode);
        var so = (await soRes.Content.ReadFromJsonAsync<SalesOrderDetailDto>(JsonOptions))!;
        var soLine = so.Revisions[0].Lines[0];

        Assert.Equal(HttpStatusCode.OK, (await sales.PostAsJsonAsync(
            $"{baseUrl}/sales-orders/{so.SalesOrderId:D}/revisions/{so.Revisions[0].RevisionId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{baseUrl}/sales-orders/{so.SalesOrderId:D}/revisions/{so.Revisions[0].RevisionId:D}/decide",
            new DecideSoRevisionRequest(ApprovalDecisions.Approved, "Approved"))).StatusCode);

        var rsvRes = await warehouse.PostAsJsonAsync($"{baseUrl}/fulfillment/reservations",
            new ReserveSalesOrderLineRequest(so.SalesOrderId, soLine.SoLineId, world.LocMainId, null, 10m, "rc01-rsv-10", "Reserve 10 PCS"));
        Assert.Equal(HttpStatusCode.Created, rsvRes.StatusCode);
        var rsv = (await rsvRes.Content.ReadFromJsonAsync<SalesReservationDto>(JsonOptions))!;

        var pickRes = await warehouse.PostAsJsonAsync($"{baseUrl}/fulfillment/picks",
            new CreatePickOrderRequest(so.SalesOrderId, false, "rc01-pck-1", "Pick 10 PCS",
                [new PickLineInputDto(rsv.StockReservationId, 10m)]));
        Assert.Equal(HttpStatusCode.Created, pickRes.StatusCode);
        var pick = (await pickRes.Content.ReadFromJsonAsync<PickOrderDto>(JsonOptions))!;

        var pickConfirmRes = await warehouse.PostAsJsonAsync(
            $"{baseUrl}/fulfillment/picks/{pick.PickOrderId:D}/confirm",
            new ConfirmPickOrderRequest([new ConfirmPickLineInputDto(pick.Lines[0].PickLineId, 10m)]));
        Assert.Equal(HttpStatusCode.OK, pickConfirmRes.StatusCode);
        var confirmedPick = (await pickConfirmRes.Content.ReadFromJsonAsync<PickOrderDto>(JsonOptions))!;

        var dispatchReq = new DispatchSalesShipmentRequest(
            confirmedPick.PickOrderId, new DateOnly(2026, 11, 16), "SJ-OUT-RC01", "Kurir RC", null,
            "rc01-shp-10", "Dispatch 10 PCS",
            [new DispatchShipmentLineInputDto(confirmedPick.Lines[0].PickLineId, 10m)]);
        var dispatchRes = await warehouse.PostAsJsonAsync($"{baseUrl}/fulfillment/shipments", dispatchReq);
        Assert.Equal(HttpStatusCode.Created, dispatchRes.StatusCode);
        var shipment = (await dispatchRes.Content.ReadFromJsonAsync<SalesShipmentDto>(JsonOptions))!;
        Assert.Equal(500_000m, shipment.TotalCogsAmount);

        var ovAfterDispatch = await Overview();
        AssertBalance(ovAfterDispatch.TrialBalance, "5100", 500_000m, 0m);
        AssertBalance(ovAfterDispatch.TrialBalance, "1300", 2_250_000m, 0m);
        Assert.Equal(45m, await OnHandAsync());

        var journalsBeforeDispatchReplay = await JournalCountAsync();
        var dispatchReplay = await warehouse.PostAsJsonAsync($"{baseUrl}/fulfillment/shipments", dispatchReq);
        Assert.Equal(HttpStatusCode.OK, dispatchReplay.StatusCode);
        Assert.Equal(journalsBeforeDispatchReplay, await JournalCountAsync());
        Assert.Equal(45m, await OnHandAsync());

        // 8. Customer invoice for the 10 dispatched (no tax): sales drafts+submits, controller posts
        var shipmentLineId = shipment.Lines[0].ShipmentLineId;
        var invRes = await sales.PostAsJsonAsync($"{baseUrl}/billing/invoices", new CreateCustomerInvoiceRequest(
            so.SalesOrderId, world.PpnOut0Id, new DateOnly(2026, 11, 17), new DateOnly(2026, 12, 1),
            null, "rc01-inv-1", "RC01 invoice",
            [new CreateCustomerInvoiceLineRequest(shipmentLineId, 10m)]));
        Assert.Equal(HttpStatusCode.Created, invRes.StatusCode);
        var invoice = (await invRes.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        Assert.Equal(800_000m, invoice.GrandTotalAmount);

        Assert.Equal(HttpStatusCode.OK, (await sales.PostAsync($"{baseUrl}/billing/invoices/{invoice.InvoiceId:D}/submit", null)).StatusCode);
        var invPostRes = await controller.PostAsJsonAsync(
            $"{baseUrl}/billing/invoices/{invoice.InvoiceId:D}/post", new PostCustomerInvoiceRequest(null));
        Assert.Equal(HttpStatusCode.OK, invPostRes.StatusCode);
        var postedInvoice = (await invPostRes.Content.ReadFromJsonAsync<CustomerInvoiceDto>(JsonOptions))!;
        var receivableId = postedInvoice.ArReceivableId!.Value;

        var ovAfterInvoice = await Overview();
        AssertBalance(ovAfterInvoice.TrialBalance, "1200", 800_000m, 0m);
        AssertBalance(ovAfterInvoice.TrialBalance, "4100", 0m, 800_000m);

        // 9. Import inbound CREDIT 300,000, confirm receipt, allocate 300,000 -> Dr 1110 / Cr 1200
        var inCsv = "ValueDate,Direction,Amount,BankReference,Counterparty,Description\n" +
                    "2026-11-18,CREDIT,300000,TRF-RC01-IN,PT Pelanggan RC,Pelunasan sebagian";
        var impRes = await controller.PostAsJsonAsync($"{baseUrl}/receivables/bank-statements/import",
            new ImportInboundBankStatementRequest(world.BankAccountId, "STMT-RC01-IN", new DateOnly(2026, 11, 18), 4_000_000m, 4_300_000m, inCsv));
        Assert.Equal(HttpStatusCode.Created, impRes.StatusCode);
        var recOv = (await controller.GetFromJsonAsync<ReceivablesOverviewResponse>(
            $"{baseUrl}/receivables/overview", JsonOptions))!;
        var inLine = recOv.InboundBankLines.Single(l => l.BankReference == "TRF-RC01-IN");

        var rctRes = await controller.PostAsJsonAsync($"{baseUrl}/receivables/receipts", new ConfirmCustomerReceiptRequest(
            inLine.BankStatementLineId, world.CustomerId, new DateOnly(2026, 11, 18), "rc01-rct-1", "Partial receipt",
            [new ReceiptAllocationLineRequest(receivableId, 300_000m)]));
        Assert.Equal(HttpStatusCode.Created, rctRes.StatusCode);

        var ovAfterReceipt = await Overview();
        AssertBalance(ovAfterReceipt.TrialBalance, "1200", 500_000m, 0m);
        AssertBalance(ovAfterReceipt.TrialBalance, "1110", 4_300_000m, 0m);
        var recOvAfter = (await controller.GetFromJsonAsync<ReceivablesOverviewResponse>(
            $"{baseUrl}/receivables/overview", JsonOptions))!;
        Assert.Equal(500_000m, recOvAfter.OpenReceivables.Single(r => r.ReceivableId == receivableId).OpenAmount);

        // 10. Reports: balanced trial balance = 7,550,000 both sides; balanced BS; 5 reconciliations variance 0
        var finalOv = await Overview();
        Assert.True(finalOv.TrialBalance.IsBalanced);
        Assert.Equal(7_550_000m, finalOv.TrialBalance.TotalEndingDebit);
        Assert.Equal(7_550_000m, finalOv.TrialBalance.TotalEndingCredit);
        Assert.True(finalOv.BalanceSheet.IsBalanced);
        Assert.Equal(5, finalOv.Reconciliations.Count);
        Assert.All(finalOv.Reconciliations, r =>
        {
            Assert.Equal("RECONCILED", r.Status);
            Assert.Equal(0m, r.VarianceAmount);
        });
        var tb = finalOv.TrialBalance;
        AssertBalance(tb, "1110", 4_300_000m, 0m);
        AssertBalance(tb, "1120", 0m, 0m);
        AssertBalance(tb, "1200", 500_000m, 0m);
        AssertBalance(tb, "1300", 2_250_000m, 0m);
        AssertBalance(tb, "2100", 0m, 1_750_000m);
        AssertBalance(tb, "2110", 0m, 0m);
        AssertBalance(tb, "3100", 0m, 5_000_000m);
        AssertBalance(tb, "4100", 0m, 800_000m);
        AssertBalance(tb, "5100", 500_000m, 0m);

        // 11. Period boundary: close -> journal rejected -> reopen -> re-close -> lock -> reopen rejected -> totals unchanged
        var reportsBase = $"{baseUrl}/reports";
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{reportsBase}/periods/{world.PeriodNovId:D}/close", new ClosePeriodRequest("RC01 close", true))).StatusCode);

        var closedPost = await controller.PostAsJsonAsync($"{baseUrl}/finance/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 20), "IDR", "Post into closed period",
            "FINANCE", "MANUAL_JOURNAL", "RC-CLOSED-1", "rc-closed-1",
            [
                new JournalLineInputDto(world.CashAccountId, "Dr", 10_000m, 0m, null, null, null),
                new JournalLineInputDto(world.EquityAccountId, "Cr", 0m, 10_000m, null, null, null)
            ]));
        Assert.True((int)closedPost.StatusCode >= 400 && (int)closedPost.StatusCode < 500,
            $"Expected 4xx for closed-period post, got {closedPost.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{reportsBase}/periods/{world.PeriodNovId:D}/reopen", new ReopenPeriodRequest("RC01 reopen"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{reportsBase}/periods/{world.PeriodNovId:D}/close", new ClosePeriodRequest("RC01 final close", true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{reportsBase}/periods/{world.PeriodNovId:D}/lock", new LockPeriodRequest("RC01 lock"))).StatusCode);

        var reopenLocked = await controller.PostAsJsonAsync(
            $"{reportsBase}/periods/{world.PeriodNovId:D}/reopen", new ReopenPeriodRequest("Bypass lock"));
        Assert.True((int)reopenLocked.StatusCode >= 400 && (int)reopenLocked.StatusCode < 500,
            $"Expected 4xx for reopen of locked period, got {reopenLocked.StatusCode}");

        var lockedOv = await Overview();
        Assert.Equal(7_550_000m, lockedOv.TrialBalance.TotalEndingDebit);
        Assert.Equal(7_550_000m, lockedOv.TrialBalance.TotalEndingCredit);
    }

    private static async Task<HttpClient> SignInAndGetCsrfAsync(WebApplicationFactory<Program> factory, string email, string password)
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

    private static async Task<RcFlowWorld> SeedRcFlowWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (buyerId, buyerEmail, _) = await fx.CreateTestUserAsync("buyer.rc@test.local", "RC Buyer", pw);
        var (warehouseId, warehouseEmail, _) = await fx.CreateTestUserAsync("warehouse.rc@test.local", "RC Warehouse", pw);
        var (apClerkId, apClerkEmail, _) = await fx.CreateTestUserAsync("apclerk.rc@test.local", "RC AP Clerk", pw);
        var (controllerId, controllerEmail, _) = await fx.CreateTestUserAsync("controller.rc@test.local", "RC Controller", pw);
        var (salesId, salesEmail, _) = await fx.CreateTestUserAsync("sales.rc@test.local", "RC Sales", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-20);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.Add(Tenant.Create(tenantAId, "NUSA", "PT Nusantara RC Flow", now));
        db.LegalEntities.Add(LegalEntity.Create(
            entityA1Id, tenantAId, "NUSA-RC", "PT Nusantara RC Flow", "01.999.888.7-011.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, buyerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, warehouseId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, apClerkId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, controllerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, salesId, now));

        foreach (var p in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept,
            PermissionCodes.InventoryRead, PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerId, p, now, null, now));
        }

        foreach (var p in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage,
            PermissionCodes.SalesRead, PermissionCodes.SalesManage, PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, warehouseId, p, now, null, now));
        }

        foreach (var p in RoleTemplates.GetPermissions(RoleTemplates.ApClerk))
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, apClerkId, p, now, null, now));
        }

        foreach (var p in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove,
            PermissionCodes.ReceivingRead, PermissionCodes.InventoryRead,
            PermissionCodes.SalesRead, PermissionCodes.SalesApprove,
            PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost,
            PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost,
            PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, controllerId, p, now, null, now));
        }

        foreach (var p in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.SalesRead, PermissionCodes.SalesManage, PermissionCodes.InventoryRead,
            PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, salesId, p, now, null, now));
        }

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.SupplierBillApprovalPolicyCode, "Bill Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, CashPolicyDefaults.SupplierPaymentReleasePolicyCode, "Payment Release", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now));
        db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));
        db.InvoiceMatchPolicies.Add(InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03 Safe", true, now));

        var cashAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1110", "Kas & Bank", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var clearAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1120", "Kliring Pembayaran Keluar", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, null, true, true, now);
        var arAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1200", "Piutang Usaha", GlAccountTypes.Asset, GlAccountCategories.AccountsReceivable, null, true, true, now);
        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnInAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1410", "PPN Masukan", GlAccountTypes.Asset, GlAccountCategories.TaxReceivable, null, true, true, now);
        var apAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2100", "Hutang Usaha", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, null, true, true, now);
        var grniAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        var ppnOutAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2210", "PPN Keluaran", GlAccountTypes.Liability, GlAccountCategories.TaxPayable, null, true, true, now);
        var equityAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "3100", "Modal Disetor", GlAccountTypes.Equity, GlAccountCategories.EquityCapital, null, true, false, now);
        var revAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "4100", "Pendapatan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, null, true, false, now);
        var cogsAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "HPP", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, true, now);
        var ppvAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5200", "PPV", GlAccountTypes.Expense, GlAccountCategories.PurchasePriceVariance, null, true, true, now);
        db.GlAccounts.AddRange(cashAcc, clearAcc, arAcc, invAcc, ppnInAcc, apAcc, grniAcc, ppnOutAcc, equityAcc, revAcc, cogsAcc, ppvAcc);

        var nov = FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11,
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now);
        db.FinancialPeriods.Add(nov);

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankMainOperating, cashAcc, "Bank", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankOutboundClearing, clearAcc, "Clear", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ArTradeControl, arAcc, "AR", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcc, "Inv", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnInputTax, ppnInAcc, "PPN-In", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ApTradeControl, apAcc, "AP", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grniAcc, "GRNI", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnOutputTax, ppnOutAcc, "PPN-Out", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.SalesRevenueDefault, revAcc, "Rev", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcc, "COGS", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PurchasePriceVariance, ppvAcc, "PPV", controllerId, now));

        var bank = BankAccount.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "BCA-RC-001", "PT Bank Central Asia Tbk",
            "Jakarta", "035-0000-001", "PT Nusantara RC Flow", "IDR",
            cashAcc.AccountId, clearAcc.AccountId, controllerId, now);
        db.BankAccounts.Add(bank);

        // Explicit 0% PPN codes (StandardTaxable -> no documentary evidence required at posting)
        var ppnIn0 = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPN-IN-0", "PPN Masukan 0%",
            TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable,
            0m, 1, 1, false, false, ppnInAcc.AccountId, new DateOnly(2026, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "RC-0PCT", now);
        var ppnOut0 = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPN-OUT-0", "PPN Keluaran 0%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable,
            0m, 1, 1, false, false, ppnOutAcc.AccountId, new DateOnly(2026, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "RC-0PCT", now);
        db.TaxCodes.AddRange(ppnIn0, ppnOut0);

        var supplier = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-RC", "PT Pemasok RC", "Pemasok RC",
            true, false, "01.555.444.3-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var customer = Partner.Create(Guid.NewGuid(), tenantAId, "CUST-RC", "PT Pelanggan RC", "Pelanggan RC",
            false, true, "01.777.666.5-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        db.Partners.AddRange(supplier, customer);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);
        var item = Item.Create(Guid.NewGuid(), tenantAId, "ITM-RC-01", "Barang RC", ItemKinds.StockedGood,
            pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        Guid locMainId;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(
                db, tenantAId, entityA1Id, now, CancellationToken.None);
            locMainId = layout.LocationsByCode["LOC-MAIN"].LocationId;
            await tx.CommitAsync();
        }

        return new RcFlowWorld(
            tenantAId, entityA1Id,
            buyerId, buyerEmail, warehouseId, warehouseEmail, apClerkId, apClerkEmail,
            controllerId, controllerEmail, salesId, salesEmail,
            supplier.PartnerId, customer.PartnerId, pcs.UomId, item.ItemId, locMainId,
            bank.BankAccountId, ppnIn0.TaxCodeId, ppnOut0.TaxCodeId, nov.PeriodId,
            cashAcc.AccountId, equityAcc.AccountId, pw);
    }
}
