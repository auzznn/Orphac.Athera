using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Inventory;

namespace Orphac.Athera.UnitTests;

public sealed class S09SupplierInvoiceMatchValuationTests
{
    private static MatchLineSourceInput BuildInput(
        decimal remainingUnbilledQty,
        decimal remainingOpenLineAmount,
        decimal receiptUnitCost,
        decimal billedQty,
        decimal invoiceUnitPrice,
        decimal poolOnHandQty,
        decimal poolCarryingValue,
        decimal ppnTaxAmount = 0m,
        decimal withholdingTaxAmount = 0m)
    {
        var avg = poolOnHandQty > 0m ? decimal.Round(poolCarryingValue / poolOnHandQty, 6, MidpointRounding.AwayFromZero) : 0m;
        return new MatchLineSourceInput(
            LineNumber: 1,
            ReceiptAccrualLineId: Guid.NewGuid(),
            ValuedAcceptanceLineId: Guid.NewGuid(),
            GoodsReceiptLineId: Guid.NewGuid(),
            PoLineId: Guid.NewGuid(),
            ItemId: Guid.NewGuid(),
            ItemCode: "ITM-S09-VALVE-01",
            ItemName: "Katup Industri PN16 50mm",
            BaseUomId: Guid.NewGuid(),
            BaseUomCode: "PCS",
            OrderedBaseQty: 100m,
            RemainingUnbilledBaseQty: remainingUnbilledQty,
            RemainingOpenLineAmount: remainingOpenLineAmount,
            ReceiptUnitCostBase: receiptUnitCost,
            PoUnitPriceFunctional: receiptUnitCost,
            BilledBaseQuantity: billedQty,
            InvoiceUnitPriceBase: invoiceUnitPrice,
            PoolOnHandQty: poolOnHandQty,
            PoolCarryingValue: poolCarryingValue,
            PoolMovingAvgCost: avg,
            PpnTaxCodeId: Guid.NewGuid(),
            PpnTaxCodeSnapshot: "PPN-IN-11",
            PpnTaxAmount: ppnTaxAmount,
            PpnBlocked: false,
            PpnBlockReason: null,
            WithholdingTaxCodeId: withholdingTaxAmount > 0m ? Guid.NewGuid() : null,
            WithholdingTaxCodeSnapshot: withholdingTaxAmount > 0m ? "PPH23-SVC-2" : null,
            WithholdingTaxAmount: withholdingTaxAmount,
            WithholdingBlocked: false,
            WithholdingBlockReason: null);
    }

    [Fact]
    public void ExactThreeWayMatch_ProducesZeroVariancesAndMatchedStatus()
    {
        var policy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        var line = BuildInput(
            remainingUnbilledQty: 100m,
            remainingOpenLineAmount: 5_000_000m,
            receiptUnitCost: 50_000m,
            billedQty: 100m,
            invoiceUnitPrice: 50_000m,
            poolOnHandQty: 40m,
            poolCarryingValue: 2_000_000m,
            ppnTaxAmount: 550_000m,
            withholdingTaxAmount: 0m);

        var result = InvoiceThreeWayMatchCalculator.Compute(policy, 5_550_000m, [line]);

        Assert.Equal(MatchDimensionOutcomes.Matched, result.DqOutcome);
        Assert.Equal(MatchDimensionOutcomes.Matched, result.PpOutcome);
        Assert.Equal(MatchDimensionOutcomes.Matched, result.BdOutcome);
        Assert.Equal(InvoiceMatchStatuses.Matched, result.OverallMatchStatus);
        Assert.False(result.IsPostingBlocked);
        Assert.False(result.RequiresApproval);
        Assert.Equal(5_000_000m, result.TotalGrniClearAmount);
        Assert.Equal(0m, result.TotalPriceVarianceAmount);
        Assert.Equal(0m, result.TotalInventoryAdjAmount);
        Assert.Equal(0m, result.TotalPnlVarianceAmount);
        Assert.Equal(5_550_000m, result.NetPayableAmount);
    }

    [Fact]
    public void PositivePriceVarianceWithPartialStock_ApportionsDecP2p013AndRequiresApproval()
    {
        var policy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        // 100 PCS @ 55.000 = 5.500.000 vs GRNI 5.000.000 -> variance +500.000
        // 40 PCS remain on hand (200.000 to Inventory 1300) and 60 PCS issued (300.000 to PPV 5200)
        var line = BuildInput(
            remainingUnbilledQty: 100m,
            remainingOpenLineAmount: 5_000_000m,
            receiptUnitCost: 50_000m,
            billedQty: 100m,
            invoiceUnitPrice: 55_000m,
            poolOnHandQty: 40m,
            poolCarryingValue: 2_000_000m,
            ppnTaxAmount: 605_000m,
            withholdingTaxAmount: 110_000m);

        var result = InvoiceThreeWayMatchCalculator.Compute(policy, 6_105_000m, [line]);

        Assert.Equal(InvoiceMatchStatuses.ApprovalRequired, result.OverallMatchStatus);
        Assert.Equal(MatchDimensionOutcomes.VarianceApprovalRequired, result.PpOutcome);
        Assert.False(result.IsPostingBlocked);
        Assert.True(result.RequiresApproval);
        Assert.Equal(500_000m, result.TotalPriceVarianceAmount);
        Assert.Equal(200_000m, result.TotalInventoryAdjAmount);
        Assert.Equal(300_000m, result.TotalPnlVarianceAmount);
        Assert.Equal(5_995_000m, result.NetPayableAmount);
        Assert.Equal(40m, result.Lines[0].CoveredBaseQuantity);
        Assert.Equal(60m, result.Lines[0].UncoveredBaseQuantity);
        Assert.Equal(55_000m, result.Lines[0].ProjectedMovingAvgCost);
    }

    [Fact]
    public void NegativePriceVariance_SplitsNegativeInventoryAndEnforcesOd04PositiveFloor()
    {
        var policy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        // Mild credit adjustment: 50.000 -> 48.000 (-200.000 total: -80.000 Inventory / -120.000 PPV)
        var mildLine = BuildInput(100m, 5_000_000m, 50_000m, 100m, 48_000m, 40m, 2_000_000m, 528_000m);
        var mildResult = InvoiceThreeWayMatchCalculator.Compute(policy, 5_328_000m, [mildLine]);

        Assert.Equal(InvoiceMatchStatuses.ApprovalRequired, mildResult.OverallMatchStatus);
        Assert.False(mildResult.IsPostingBlocked);
        Assert.Equal(-200_000m, mildResult.TotalPriceVarianceAmount);
        Assert.Equal(-80_000m, mildResult.TotalInventoryAdjAmount);
        Assert.Equal(-120_000m, mildResult.TotalPnlVarianceAmount);
        Assert.Equal(48_000m, mildResult.Lines[0].ProjectedMovingAvgCost);

        // Extreme negative variance: pool carrying 2.000.000 would drop <= 0 -> OD-04 floor blocks
        // Invoice 100 @ 10.000 = 1.000.000 vs GRNI 5.000.000 (-4.000.000 * 40% = -1.600.000 still positive 400k),
        // so test 80 PCS on hand (4.000.000 carrying) with 100 billed @ 100 (-4.990.000 * 80% = -3.992.000) vs carrying 3.900.000
        var extremeStepEx = Assert.Throws<InvalidOperationException>(() =>
            ValuationPool.SimulateInvoicePriceAdjustment(
                ValuationPoolStatuses.Active,
                previousQty: 80m,
                previousCarryingValue: 3_500_000m,
                previousUnitCost: 43_750m,
                currentSequence: 2L,
                billedBaseQuantity: 100m,
                totalPriceDifferenceAmount: -4_990_000m));
        Assert.Contains("od04_valuation_floor_breached", extremeStepEx.Message, StringComparison.Ordinal);

        var blockedLine = BuildInput(100m, 5_000_000m, 50_000m, 100m, 100m, 80m, 3_500_000m, 1_100m);
        var blockedCalc = InvoiceThreeWayMatchCalculator.Compute(policy, 11_100m, [blockedLine]);
        Assert.True(blockedCalc.IsPostingBlocked);
        Assert.Equal(InvoiceMatchStatuses.PostingBlocked, blockedCalc.OverallMatchStatus);
        Assert.Contains(blockedCalc.BlockingReasons, r => r.Contains("od04_valuation_floor_breached", StringComparison.Ordinal));
    }

    [Fact]
    public void ZeroOnHandStock_RoutesOneHundredPercentVarianceToPpv5200()
    {
        var policy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        var line = BuildInput(
            remainingUnbilledQty: 100m,
            remainingOpenLineAmount: 5_000_000m,
            receiptUnitCost: 50_000m,
            billedQty: 100m,
            invoiceUnitPrice: 55_000m,
            poolOnHandQty: 0m,
            poolCarryingValue: 0m,
            ppnTaxAmount: 605_000m);

        var result = InvoiceThreeWayMatchCalculator.Compute(policy, 6_105_000m, [line]);

        Assert.False(result.IsPostingBlocked);
        Assert.Equal(500_000m, result.TotalPriceVarianceAmount);
        Assert.Equal(0m, result.TotalInventoryAdjAmount);
        Assert.Equal(500_000m, result.TotalPnlVarianceAmount);
        Assert.Equal(0m, result.Lines[0].CoveredBaseQuantity);
        Assert.Equal(100m, result.Lines[0].UncoveredBaseQuantity);
    }

    [Fact]
    public void OverbillingQuantityDq_TriggersPostingBlockedRegardlessOfApproval()
    {
        var policy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        var line = BuildInput(
            remainingUnbilledQty: 40m,
            remainingOpenLineAmount: 2_000_000m,
            receiptUnitCost: 50_000m,
            billedQty: 45m,
            invoiceUnitPrice: 50_000m,
            poolOnHandQty: 40m,
            poolCarryingValue: 2_000_000m,
            ppnTaxAmount: 247_500m);

        var result = InvoiceThreeWayMatchCalculator.Compute(policy, 2_497_500m, [line]);

        Assert.True(result.IsPostingBlocked);
        Assert.Equal(MatchDimensionOutcomes.Blocked, result.DqOutcome);
        Assert.Equal(InvoiceMatchStatuses.PostingBlocked, result.OverallMatchStatus);
        Assert.Contains(result.BlockingReasons, r => r.Contains("dq_exceeds_unbilled_accepted_capacity", StringComparison.Ordinal));
    }

    [Fact]
    public void BalanceDifferenceBd_BlockedUnderSafeDefaultButAllowedWithinReviewedTolerance()
    {
        var safePolicy = InvoiceMatchPolicy.CreateSafeDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03", true, DateTimeOffset.UtcNow);

        var exactLine = BuildInput(100m, 5_000_000m, 50_000m, 100m, 50_000m, 40m, 2_000_000m, 550_000m);

        // Under safe default (BD write-off limit = 0), a Rp 50 header discrepancy blocks posting
        var safeBlocked = InvoiceThreeWayMatchCalculator.Compute(safePolicy, 5_550_050m, [exactLine]);
        Assert.True(safeBlocked.IsPostingBlocked);
        Assert.Equal(MatchDimensionOutcomes.Blocked, safeBlocked.BdOutcome);
        Assert.Equal(InvoiceMatchStatuses.PostingBlocked, safeBlocked.OverallMatchStatus);

        // Under reviewed internal policy (2% PP tolerance, Rp 1.000 BD write-off account), small PP + BD auto-settle as WITHIN_TOLERANCE
        var reviewedPolicy = InvoiceMatchPolicy.CreateReviewedForVerification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "REVIEWED_TOL_VERIFICATION",
            "Reviewed Verification Policy",
            ppTolerancePercent: 2.0m,
            ppToleranceAmount: 100_000m,
            bdWriteOffLimitAmount: 1_000m,
            bdWriteOffAccountId: Guid.NewGuid(),
            isDefault: true,
            DateTimeOffset.UtcNow);

        // Small PP: +500/unit on 100 PCS = +50.000 (1.0% <= 2.0% and <= 100.000), PPN 555.500, BD = +100 (<= 1.000)
        var tolLine = BuildInput(100m, 5_000_000m, 50_000m, 100m, 50_500m, 40m, 2_000_000m, 555_500m);
        var tolResult = InvoiceThreeWayMatchCalculator.Compute(reviewedPolicy, 5_605_600m, [tolLine]);

        Assert.False(tolResult.IsPostingBlocked);
        Assert.False(tolResult.RequiresApproval);
        Assert.Equal(MatchDimensionOutcomes.WithinTolerance, tolResult.PpOutcome);
        Assert.Equal(MatchDimensionOutcomes.WithinTolerance, tolResult.BdOutcome);
        Assert.Equal(InvoiceMatchStatuses.WithinTolerance, tolResult.OverallMatchStatus);
        Assert.Equal(100m, tolResult.BdWriteOffAmount);
    }

    [Fact]
    public void Od03AndOd08DomainGuards_RejectUnreviewedTolerancesAndNonPoBills()
    {
        var od03Ex = Assert.Throws<InvalidOperationException>(() =>
            InvoiceMatchPolicy.EnsurePublicSafeValues(1m, 0m, 0m, 0m, 0m, null));
        Assert.Contains("od03_unreviewed_tolerance_policy_blocked", od03Ex.Message, StringComparison.Ordinal);

        var od08Ex = Assert.Throws<InvalidOperationException>(() =>
            SupplierBill.CreateDraft(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "BILL-DRF-0001", SupplierBillTypes.NonPoExpense,
                Guid.NewGuid(), Guid.NewGuid(), "PO-001",
                Guid.NewGuid(), "SUP-1", "Supplier", null,
                "INV-01", null, null,
                new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10),
                "IDR", 100_000m, null, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Contains("od08", od08Ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
