using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;

namespace Orphac.Athera.UnitTests;

public sealed class S07ValuedAcceptanceValuationTests
{
    [Fact]
    public void MovingAverageValuation_TwoBatchesAndNonTerminatingDivision_PreservesCarryingValueAndPrecision()
    {
        var now = DateTimeOffset.UtcNow;
        var pool = ValuationPool.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "IDR",
            Guid.NewGuid(),
            now);

        // Batch 1: 10 PCS @ IDR 100,000 = IDR 1,000,000 -> MAC 100,000.000000
        var step1 = pool.ApplyReceiptValuation(10m, 1_000_000m, now.AddMinutes(1));
        Assert.Equal(10m, pool.ValuedBaseQuantity);
        Assert.Equal(1_000_000m, pool.CarryingValueAmount);
        Assert.Equal(100_000m, pool.MovingAverageUnitCost);
        Assert.Equal(1L, step1.EntrySequence);

        // Batch 2: 10 PCS @ IDR 130,000 = IDR 1,300,000 -> (2,300,000 / 20) = IDR 115,000.000000
        var step2 = pool.ApplyReceiptValuation(10m, 1_300_000m, now.AddMinutes(2));
        Assert.Equal(20m, pool.ValuedBaseQuantity);
        Assert.Equal(2_300_000m, pool.CarryingValueAmount);
        Assert.Equal(115_000m, pool.MovingAverageUnitCost);
        Assert.Equal(2L, step2.EntrySequence);
        Assert.Equal(100_000m, step2.PreviousMovingAverageCost);

        // Non-terminating division: 3 units for total value 100,000 -> MAC = 33,333.333333, carrying stays exact 100,000.0000
        var oddPool = ValuationPool.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "IDR",
            Guid.NewGuid(),
            now);

        var oddStep = oddPool.ApplyReceiptValuation(3m, 100_000m, now.AddMinutes(3));
        Assert.Equal(3m, oddStep.NewBaseQuantity);
        Assert.Equal(100_000m, oddStep.NewCarryingValue);
        Assert.Equal(33_333.333333m, oddStep.NewMovingAverageCost);
    }

    [Fact]
    public void StockPosition_UnrestrictedVsRestrictedStatuses_MaintainExactAvailabilityInvariants()
    {
        var now = DateTimeOffset.UtcNow;
        var pos = StockPosition.CreateInitial(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);

        pos.ApplyInboundReceipt(10m, StockStatuses.Unrestricted, now.AddMinutes(1));
        Assert.Equal(10m, pos.UnrestrictedBaseQuantity);
        Assert.Equal(10m, pos.TotalValuedOnHandBaseQuantity);
        Assert.Equal(10m, pos.AvailableBaseQuantity);

        pos.ApplyInboundReceipt(4m, StockStatuses.QualityInspection, now.AddMinutes(2));
        Assert.Equal(4m, pos.QualityInspectionBaseQuantity);
        Assert.Equal(14m, pos.TotalValuedOnHandBaseQuantity);
        Assert.Equal(10m, pos.AvailableBaseQuantity);

        pos.ApplyInboundReceipt(2m, StockStatuses.Blocked, now.AddMinutes(3));
        Assert.Equal(2m, pos.BlockedBaseQuantity);
        Assert.Equal(16m, pos.TotalValuedOnHandBaseQuantity);
        Assert.Equal(10m, pos.AvailableBaseQuantity);
    }

    [Fact]
    public void Od04Guards_BlockZeroOrNegativeValueAndAnomalousPools()
    {
        var zeroEx = Assert.Throws<InvalidOperationException>(() =>
            ValuationPool.SimulateReceiptValuation(ValuationPoolStatuses.Active, 0m, 0m, 0m, 0L, 5m, 0m));
        Assert.Contains("od04_zero_or_negative_acceptance_value_blocked", zeroEx.Message, StringComparison.Ordinal);

        var orphanEx = Assert.Throws<InvalidOperationException>(() =>
            ValuationPool.SimulateReceiptValuation(ValuationPoolStatuses.Active, 0m, 50_000m, 0m, 1L, 5m, 100_000m));
        Assert.Contains("od04_anomalous_valuation_pool_blocked", orphanEx.Message, StringComparison.Ordinal);

        var blockedEx = Assert.Throws<InvalidOperationException>(() =>
            ValuationPool.SimulateReceiptValuation(ValuationPoolStatuses.Blocked, 10m, 100_000m, 10_000m, 1L, 5m, 50_000m));
        Assert.Contains("od04_anomalous_valuation_pool_blocked", blockedEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GoodsReceiptLine_RecordValuedAcceptance_ConservesBucketsAndBlocksOvershootOrFractionalDiscrete()
    {
        var now = DateTimeOffset.UtcNow;
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var revId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var pcsId = Guid.NewGuid();

        var poLine = PurchaseOrderLine.Create(
            Guid.NewGuid(),
            tid,
            eid,
            poId,
            revId,
            1,
            "L001",
            itemId,
            "ITM-STL",
            "Pelat Baja",
            ItemKinds.StockedGood,
            pcsId,
            "PCS",
            pcsId,
            "PCS",
            1m,
            20m,
            20m,
            100_000m,
            null,
            null,
            null,
            null,
            2_000_000m,
            0m,
            0m,
            0m,
            false,
            null);

        var grLine = GoodsReceiptLine.CreateArrival(
            Guid.NewGuid(),
            tid,
            eid,
            Guid.NewGuid(),
            1,
            poLine,
            pcsId,
            "PCS",
            1m,
            10m,
            10m,
            20m,
            flagForInspection: true,
            DiscrepancyReasonCodes.None,
            null,
            null,
            now);

        // Release 8 PCS and hold 2 PCS in dock inspection
        grLine.ApplyDockDisposition(0m, 2m, 8m, 0m, 20m, 0, DiscrepancyReasonCodes.DamagedPackaging, "Hold 2 for packaging inspection", null, now.AddMinutes(1));

        // Fractional acceptance for discrete UoM (precisionScale=0) rejected
        var fracEx = Assert.Throws<InvalidOperationException>(() =>
            grLine.RecordValuedAcceptance(2.5m, baseUomPrecisionScale: 0, now.AddMinutes(2)));
        Assert.Contains("fractional_discrete_base_quantity", fracEx.Message, StringComparison.Ordinal);

        // Exceeding released (8) with 9 rejected (held qty 2 is excluded)
        var capEx = Assert.Throws<InvalidOperationException>(() =>
            grLine.RecordValuedAcceptance(9m, baseUomPrecisionScale: 0, now.AddMinutes(2)));
        Assert.Contains("acceptance_exceeds_released_quantity", capEx.Message, StringComparison.Ordinal);

        // Partial acceptance of 5 PCS conserves all buckets cleanly
        grLine.RecordValuedAcceptance(5m, baseUomPrecisionScale: 0, now.AddMinutes(3));
        Assert.Equal(5m, grLine.ValuedAcceptedBaseQuantity);
        Assert.Equal(8m, grLine.DockReleasedBaseQuantity);
        Assert.Equal(2m, grLine.DockHeldBaseQuantity);
        // Unresolved custody = held(2) + unreleased-from-gate2(8 - 5 = 3) = 5; delivered(10) = rejected(0) + valued(5) + unresolved(5)
        Assert.Equal(5m, grLine.UnresolvedCustodyBaseQuantity);
        grLine.AssertConservation();

        var accrual = ReceiptAccrual.Create(
            Guid.NewGuid(),
            tid,
            eid,
            "GRNI-2026-00001",
            Guid.NewGuid(),
            grLine.GoodsReceiptId,
            poId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 11, 10),
            "IDR",
            500_000m,
            Guid.NewGuid(),
            now);
        Assert.Equal(ReceiptAccrualStatuses.Open, accrual.Status);
        Assert.Equal(500_000m, accrual.RemainingOpenAmount);
    }
}
