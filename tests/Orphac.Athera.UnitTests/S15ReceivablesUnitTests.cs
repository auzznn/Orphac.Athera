using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Receivables;

namespace Orphac.Athera.UnitTests;

public sealed class S15ReceivablesUnitTests
{
    [Fact]
    public void CustomerReceipt_ConservesAllocatedAndUnappliedAmountsAndGuardsOverAllocation()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var rct = CustomerReceipt.CreateConfirmed(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "RCT-2026-00001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PT-CUST-001", "PT Graha Ritel",
            new DateOnly(2026, 11, 19), "BNK-CR-001", "IDR",
            500_000m, Guid.NewGuid(), "JRN-2026-11-00040", "idem-rct-1", null, Guid.NewGuid(), now);

        Assert.Equal(500_000m, rct.UnappliedAmount);
        Assert.Equal(CustomerReceiptStatuses.Confirmed, rct.Status);

        rct.ApplyAllocation(200_000m, now.AddMinutes(1));
        Assert.Equal(200_000m, rct.AllocatedAmount);
        Assert.Equal(300_000m, rct.UnappliedAmount);
        Assert.Equal(CustomerReceiptStatuses.PartiallyAllocated, rct.Status);

        Assert.Throws<InvalidOperationException>(() => rct.ApplyAllocation(300_001m, now.AddMinutes(2)));

        rct.ApplyAllocation(300_000m, now.AddMinutes(3));
        Assert.Equal(500_000m, rct.AllocatedAmount);
        Assert.Equal(0m, rct.UnappliedAmount);
        Assert.Equal(CustomerReceiptStatuses.Allocated, rct.Status);
    }

    [Fact]
    public void CustomerReceivable_AppliesAndReversesReceiptAllocationsCleanly()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var ar = CustomerReceivable.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "AR-2026-00001", Guid.NewGuid(), "INV-2026-00001",
            Guid.NewGuid(), "PT-CUST-001", "PT Graha Ritel",
            new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "IDR",
            333_000m, Guid.NewGuid(), "JRN-2026-11-00010", now);

        ar.ApplyReceipt(200_000m, now.AddMinutes(1));
        Assert.Equal(200_000m, ar.ReceivedAmount);
        Assert.Equal(133_000m, ar.OpenAmount);
        Assert.Equal(ReceivableStatuses.PartiallySettled, ar.Status);

        Assert.Throws<InvalidOperationException>(() => ar.ApplyReceipt(134_000m, now.AddMinutes(2)));

        ar.ReverseReceipt(200_000m, now.AddMinutes(3));
        Assert.Equal(0m, ar.ReceivedAmount);
        Assert.Equal(333_000m, ar.OpenAmount);
        Assert.Equal(ReceivableStatuses.Open, ar.Status);
    }

    [Fact]
    public void ArAgingBuckets_ClassifiesCurrentAndPastDueRangesDeterministically()
    {
        var asOf = new DateOnly(2026, 11, 19);

        Assert.Equal((ArAgingBuckets.Current, 0), ArAgingBuckets.Classify(new DateOnly(2026, 11, 25), asOf));
        Assert.Equal((ArAgingBuckets.Current, 0), ArAgingBuckets.Classify(new DateOnly(2026, 11, 19), asOf));
        Assert.Equal((ArAgingBuckets.Days1To30, 15), ArAgingBuckets.Classify(new DateOnly(2026, 11, 4), asOf));
        Assert.Equal((ArAgingBuckets.Days31To60, 45), ArAgingBuckets.Classify(new DateOnly(2026, 10, 5), asOf));
        Assert.Equal((ArAgingBuckets.Days61To90, 75), ArAgingBuckets.Classify(new DateOnly(2026, 9, 5), asOf));
        Assert.Equal((ArAgingBuckets.Over90, 110), ArAgingBuckets.Classify(new DateOnly(2026, 8, 1), asOf));
    }
}
