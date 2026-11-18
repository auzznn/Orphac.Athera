using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.UnitTests;

public sealed class S10SupplierReturnMatrixUnitTests
{
    private static SupplierReturnMatrixLineInput BuildMatrixLine(
        decimal qty,
        decimal eligibleCap,
        decimal authUnitCost = 100_000m,
        decimal poolQty = 10m,
        decimal poolValue = 1_000_000m,
        decimal grniRemQty = 10m,
        decimal grniRemAmt = 1_000_000m,
        decimal billQty = 10m,
        decimal billNet = 1_000_000m,
        decimal billPpn = 110_000m,
        decimal billWh = 0m,
        decimal priorCredQty = 0m,
        decimal priorCredSub = 0m,
        decimal priorCredPpn = 0m)
    {
        var mac = poolQty > 0m ? decimal.Round(poolValue / poolQty, 6, MidpointRounding.AwayFromZero) : 0m;
        return new SupplierReturnMatrixLineInput(
            GoodsReceiptLineId: Guid.NewGuid(),
            ValuedAcceptanceLineId: Guid.NewGuid(),
            ReceiptAccrualLineId: Guid.NewGuid(),
            SupplierBillLineId: Guid.NewGuid(),
            PoLineId: Guid.NewGuid(),
            LineageLineKey: "L001",
            ItemId: Guid.NewGuid(),
            ItemCode: "ITM-TEST-01",
            ItemName: "Industrial Seal",
            BaseUomId: Guid.NewGuid(),
            BaseUomCode: "PCS",
            SourceLocationId: Guid.NewGuid(),
            SourceStockLotId: null,
            SourceStockStatus: StockStatuses.Unrestricted,
            ReturnedBaseQuantity: qty,
            AuthorizedUnitCostBase: authUnitCost,
            PoolValuedQty: poolQty,
            PoolCarryingValue: poolValue,
            PoolMovingAvgCost: mac,
            PoolSequence: 1L,
            UnbilledGrniRemainingQty: grniRemQty,
            UnbilledGrniRemainingAmt: grniRemAmt,
            BillLineNetAmount: billNet,
            BillLineBilledQty: billQty,
            PriorCreditedQty: priorCredQty,
            PriorCreditedSubtotal: priorCredSub,
            PriorCreditedPpn: priorCredPpn,
            BillLinePpnAmount: billPpn,
            BillLineWithholdingAmount: billWh,
            EligibleCapacityQty: eligibleCap);
    }

    [Fact]
    public void Matrix_M1_UnvaluedDockRejection_ZeroGlZeroInventory_AndReplacementSupported()
    {
        var line = BuildMatrixLine(qty: 4m, eligibleCap: 6m, authUnitCost: 75_000m);

        var result = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Closed,
            taxReturnNoteNumber: null,
            [line]);

        Assert.Equal("M1", result.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.Supported, result.Disposition);
        Assert.False(result.RequiresPeriodOpen);
        Assert.Equal(4m, result.TotalReturnedBaseQuantity);
        Assert.Equal(0m, result.TotalInventoryCreditAmount);
        Assert.Equal(0m, result.TotalGrniReversalAmount);
        Assert.Equal(0m, result.TotalCreditNoteAmount);
        Assert.Empty(result.JournalSuggestions);
        Assert.True(result.Lines[0].CreatesReplacement);
    }

    [Fact]
    public void Matrix_M2_ValuedUnbilledReturn_ComputesGrniMacAndPpvDelta()
    {
        // Pool MAC shifted to 105,000 while original receipt unit cost is 100,000 -> -50,000 PPV (Dr 5200)
        var line = BuildMatrixLine(
            qty: 10m,
            eligibleCap: 10m,
            authUnitCost: 100_000m,
            poolQty: 20m,
            poolValue: 2_100_000m,
            grniRemQty: 10m,
            grniRemAmt: 1_000_000m);

        var result = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedUnbilledReturn,
            ReturnResolutionModes.CloseWithoutReplacement,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Open,
            taxReturnNoteNumber: null,
            [line]);

        Assert.Equal("M2", result.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.Supported, result.Disposition);
        Assert.Equal(1_000_000m, result.TotalGrniReversalAmount);
        Assert.Equal(1_050_000m, result.TotalInventoryCreditAmount);
        Assert.Equal(-50_000m, result.TotalPpvDifferenceAmount);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "DEBIT" && j.AccountCode == "2110" && j.Amount == 1_000_000m);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "CREDIT" && j.AccountCode == "1300" && j.Amount == 1_050_000m);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "DEBIT" && j.AccountCode == "5200" && j.Amount == 50_000m);
    }

    [Fact]
    public void Matrix_M3_ValuedBilledReturn_ApCreditNoteAndNotaRetur()
    {
        var line = BuildMatrixLine(
            qty: 5m,
            eligibleCap: 10m,
            authUnitCost: 100_000m,
            poolQty: 10m,
            poolValue: 1_000_000m,
            billQty: 10m,
            billNet: 1_000_000m,
            billPpn: 110_000m,
            billWh: 0m);

        var result = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Open,
            taxReturnNoteNumber: "NR-2026-0001",
            [line]);

        Assert.Equal("M3", result.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.Supported, result.Disposition);
        Assert.True(result.RequiresNotaRetur);
        Assert.Equal(500_000m, result.TotalCreditSubtotalAmount);
        Assert.Equal(55_000m, result.TotalPpnReversalAmount);
        Assert.Equal(555_000m, result.TotalCreditNoteAmount);
        Assert.Equal(500_000m, result.TotalInventoryCreditAmount);
        Assert.Equal(0m, result.TotalPpvDifferenceAmount);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "DEBIT" && j.AccountCode == "2100" && j.Amount == 555_000m);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "CREDIT" && j.AccountCode == "1300" && j.Amount == 500_000m);
        Assert.Contains(result.JournalSuggestions, j => j.Side == "CREDIT" && j.AccountCode == "1410" && j.Amount == 55_000m);
    }

    [Fact]
    public void Matrix_M4_M5_M6_M7_FailClosedDispositions()
    {
        var baseLine = BuildMatrixLine(qty: 2m, eligibleCap: 10m);

        var m4 = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ReplacementOnly,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Open,
            taxReturnNoteNumber: null,
            [baseLine]);
        Assert.Equal("M4", m4.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.BlockedPolicyOd05, m4.Disposition);
        Assert.Equal("od05_billed_replacement_policy_blocked", m4.BlockingCode);

        var m5 = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            payablePaidAmount: 250_000m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Open,
            taxReturnNoteNumber: "NR-2026-0002",
            [baseLine]);
        Assert.Equal("M5", m5.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.BlockedPolicyOd05, m5.Disposition);
        Assert.Equal("od05_paid_bill_return_policy_blocked", m5.BlockingCode);

        var whLine = BuildMatrixLine(qty: 2m, eligibleCap: 10m, billWh: 20_000m);
        var m6 = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedBilledReturn,
            ReturnResolutionModes.ApCreditNote,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Open,
            taxReturnNoteNumber: "NR-2026-0003",
            [whLine]);
        Assert.Equal("M6", m6.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.BlockedPolicyOd07, m6.Disposition);
        Assert.Equal("od07_withholding_return_reversal_blocked", m6.BlockingCode);

        var m7 = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.ValuedUnbilledReturn,
            ReturnResolutionModes.CloseWithoutReplacement,
            payablePaidAmount: 0m,
            fiscalPeriodStatus: FinancialPeriodStatuses.Closed,
            taxReturnNoteNumber: null,
            [baseLine]);
        Assert.Equal("M7", m7.MatrixCode);
        Assert.Equal(ReturnMatrixDispositions.BlockedClosedPeriod, m7.Disposition);
        Assert.Equal("period_closed", m7.BlockingCode);
    }

    [Fact]
    public void ReplacementAuthorization_EnforcesZeroCommercialPricePositiveInventoryCostAndQuantityBounds()
    {
        var now = DateTimeOffset.UtcNow;
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var uomId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var poLineId = Guid.NewGuid();

        var partner = Partner.Create(supplierId, tid, "SUP-01", "PT Test Supplier", "Test Supplier", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now);
        var po = PurchaseOrder.CreateDraft(
            poId, tid, eid, "DPO-01", supplierId, "IDR", "IDR",
            new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 20), "Gudang Jakarta", 30, userId, now);
        po.MarkSubmitted("PO-2026-00001", now);

        var matrix = SupplierReturnMatrixCalculator.Evaluate(
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            0m,
            FinancialPeriodStatuses.Open,
            null,
            [new SupplierReturnMatrixLineInput(
                Guid.NewGuid(), null, null, null, poLineId, "L001", itemId, "ITM-1", "Item 1",
                uomId, "PCS", null, null, null, 6m, 75_000m, 0m, 0m, 0m, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 6m)]);

        var ret = SupplierReturn.CreateDraft(
            Guid.NewGuid(), tid, eid, "DRT-01",
            SupplierReturnCaseTypes.UnvaluedDockRejection,
            ReturnResolutionModes.ReplacementOnly,
            matrix, po, partner, Guid.NewGuid(), "GRN-01", null, null, null, null,
            new DateOnly(2026, 11, 10), SupplierReturnReasonCodes.DamagedOrDefective, null, null, null, userId, now);

        var line = SupplierReturnLine.Create(Guid.NewGuid(), tid, eid, ret.SupplierReturnId, 1, matrix.Lines[0], null);

        var zeroCostEx = Assert.Throws<InvalidOperationException>(() =>
            ReplacementAuthorization.CreateFromReturnLine(
                Guid.NewGuid(), tid, eid, "RPA-2026-00001", ret, line, 0m, userId, userId, now));
        Assert.Contains("od04_zero_or_negative_replacement_cost_blocked", zeroCostEx.Message);

        var auth = ReplacementAuthorization.CreateFromReturnLine(
            Guid.NewGuid(), tid, eid, "RPA-2026-00001", ret, line, 75_000m, userId, userId, now);
        Assert.Equal(0m, auth.CommercialIncrementalUnitPrice);
        Assert.Equal(75_000m, auth.AuthorizedInventoryUnitCostBase);
        Assert.Equal(6m, auth.AuthorizedBaseQuantity);
        Assert.Equal(6m, auth.RemainingBaseQuantity);

        var overEx = Assert.Throws<InvalidOperationException>(() => auth.RecordDelivered(7m, now));
        Assert.Contains("replacement_entitlement_exceeded", overEx.Message);

        auth.RecordDelivered(4m, now);
        Assert.Equal(ReplacementAuthorizationStatuses.PartiallyReceived, auth.Status);
        Assert.Equal(2m, auth.RemainingBaseQuantity);
        auth.RecordAccepted(4m, now);

        auth.RecordDelivered(2m, now);
        auth.RecordAccepted(2m, now);
        Assert.Equal(ReplacementAuthorizationStatuses.Fulfilled, auth.Status);
        Assert.Equal(0m, auth.RemainingBaseQuantity);
    }

    [Fact]
    public void ValuationPool_ApplyPurchaseReturn_EnforcesOd04PartialFloorAndCleanZeroDrain()
    {
        var now = DateTimeOffset.UtcNow;
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var uomId = Guid.NewGuid();

        var pool = ValuationPool.CreateInitial(Guid.NewGuid(), tid, eid, itemId, "IDR", uomId, now);
        pool.ApplyReceiptValuation(10m, 1_000_000m, now);

        var partial = pool.ApplyPurchaseReturn(4m, now);
        Assert.Equal(400_000m, partial.IssuedCarryingAmount);
        Assert.Equal(100_000m, partial.AppliedUnitCost);
        Assert.Equal(6m, pool.ValuedBaseQuantity);
        Assert.Equal(600_000m, pool.CarryingValueAmount);

        var drained = pool.ApplyPurchaseReturn(6m, now);
        Assert.Equal(600_000m, drained.IssuedCarryingAmount);
        Assert.Equal(0m, pool.ValuedBaseQuantity);
        Assert.Equal(0m, pool.CarryingValueAmount);
        Assert.Equal(0m, pool.MovingAverageUnitCost);

        // Simulate OD-04 floor: pool carrying value cannot collapse to <= 0 while positive units remain
        var floorEx = Assert.Throws<InvalidOperationException>(() =>
            ValuationPool.SimulatePurchaseReturn(
                ValuationPoolStatuses.Active,
                1_000_000m,
                0.0001m,
                0.000001m,
                1L,
                500_000m));
        Assert.Contains("od04_valuation_floor_breached", floorEx.Message);
    }

    [Fact]
    public void ReceiptAccrualAndPayableItem_ConserveReturnedAndCreditedTotals()
    {
        var now = DateTimeOffset.UtcNow;
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accrualId = Guid.NewGuid();

        var accrual = ReceiptAccrual.Create(
            accrualId, tid, eid, "ACC-2026-0001", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 10), "IDR", 1_000_000m, userId, now);
        var accLine = ReceiptAccrualLine.Create(
            Guid.NewGuid(), tid, eid, accrualId, 1, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 10m, 100_000m, 1_000_000m);

        var retAmt = accLine.ApplyUnbilledReturn(4m);
        accrual.ApplyLineClearings([accLine], now);

        Assert.Equal(400_000m, retAmt);
        Assert.Equal(4m, accLine.ReturnedBaseQuantity);
        Assert.Equal(6m, accLine.RemainingUnbilledBaseQuantity);
        Assert.Equal(400_000m, accrual.ReturnedAmount);
        Assert.Equal(600_000m, accrual.RemainingOpenAmount);
        Assert.Equal(ReceiptAccrualStatuses.PartiallyCleared, accrual.Status);

        var retAll = accLine.ApplyUnbilledReturn(6m);
        accrual.ApplyLineClearings([accLine], now);
        Assert.Equal(600_000m, retAll);
        Assert.Equal(0m, accrual.RemainingOpenAmount);
        Assert.Equal(ReceiptAccrualStatuses.Cleared, accrual.Status);

        var bill = SupplierBill.CreateDraft(
            Guid.NewGuid(), tid, eid, "DBIL-01", SupplierBillTypes.PoThreeWayMatch,
            Guid.NewGuid(), Guid.NewGuid(), "PO-01", Guid.NewGuid(), "SUP-1", "PT Supp", null,
            "INV-01", null, null, new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10), "IDR", 1_110_000m, null, userId, now);
        var policy = InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tid, eid, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact", true, now);
        var comp = InvoiceThreeWayMatchCalculator.Compute(
            policy,
            1_110_000m,
            [new MatchLineSourceInput(
                1, accLine.ReceiptAccrualLineId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "ITM-1", "Item 1", Guid.NewGuid(), "PCS", 10m, 10m, 1_000_000m, 100_000m, 100_000m,
                10m, 100_000m, 10m, 1_000_000m, 100_000m,
                Guid.NewGuid(), "PPN-IN-11", 110_000m, false, null, null, null, 0m, false, null)]);
        bill.ApplyMatchComputation(comp, now);

        var payable = PayableItem.Create(Guid.NewGuid(), tid, eid, "AP-2026-0001", bill, Guid.NewGuid(), new DateOnly(2026, 11, 10), userId, now);
        payable.ApplySupplierCredit(333_000m, now);
        Assert.Equal(333_000m, payable.CreditedAmount);
        Assert.Equal(777_000m, payable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.Open, payable.Status);

        payable.ApplySupplierCredit(777_000m, now);
        Assert.Equal(1_110_000m, payable.CreditedAmount);
        Assert.Equal(0m, payable.RemainingOpenAmount);
        Assert.Equal(PayableStatuses.Credited, payable.Status);
    }
}
