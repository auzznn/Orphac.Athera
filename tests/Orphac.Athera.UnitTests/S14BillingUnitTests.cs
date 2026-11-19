using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.UnitTests;

public sealed class S14BillingUnitTests
{
    [Fact]
    public void OutputTaxCalculator_ComputesStandard11PercentAndSpecialDppAndExemptExactly()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var tid = Guid.NewGuid();
        var eid = Guid.NewGuid();
        var taxAcct = Guid.NewGuid();

        var ppn11 = TaxCode.Create(
            Guid.NewGuid(), tid, eid, "PPN-OUT-11", "PPN Keluaran 11%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable,
            11m, 1, 1, true, false, taxAcct, new DateOnly(2025, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "OD-07", now);

        var ppnExempt = TaxCode.Create(
            Guid.NewGuid(), tid, eid, "PPN-OUT-EXEMPT", "PPN Dibebaskan 0%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.Exempt,
            0m, 1, 1, true, false, null, new DateOnly(2025, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "OD-07", now);

        var (dpp11, tax11, rate11) = BillingTaxCalculator.ComputeOutputTax(600_000m, ppn11);
        Assert.Equal(600_000m, dpp11);
        Assert.Equal(66_000m, tax11);
        Assert.Equal(11m, rate11);
        Assert.True(BillingTaxCalculator.RequiresEvidence(ppn11));

        var (dppEx, taxEx, rateEx) = BillingTaxCalculator.ComputeOutputTax(600_000m, ppnExempt);
        Assert.Equal(600_000m, dppEx);
        Assert.Equal(0m, taxEx);
        Assert.Equal(0m, rateEx);
        Assert.True(BillingTaxCalculator.RequiresEvidence(ppnExempt));
    }

    [Fact]
    public void CustomerInvoice_GuardsLifecycleTransitionsAndDueDateOrder()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var inv = CustomerInvoice.CreateDraft(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-2026-00001", InvoiceDocumentTypes.Invoice, null, null,
            Guid.NewGuid(), "SO-2026-00001", Guid.NewGuid(), "CUST-01", "PT Pelanggan", "01.234.567.8-001.000",
            new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "IDR",
            300_000m, 33_000m, Guid.NewGuid(), "PPN-OUT-11", TaxTreatmentClasses.StandardTaxable, 11m, true,
            null, "PPN-OUT-11 (11%)", "idem-1", null, Guid.NewGuid(), now);

        Assert.Equal(333_000m, inv.GrandTotalAmount);
        Assert.Equal(CustomerInvoiceStatuses.Draft, inv.Status);

        inv.Submit(Guid.NewGuid(), now.AddMinutes(1));
        Assert.Equal(CustomerInvoiceStatuses.Submitted, inv.Status);

        inv.MarkPosted(Guid.NewGuid(), "JRN-2026-11-00010", Guid.NewGuid(), "FP-010.001-26.00000001", Guid.NewGuid(), now.AddMinutes(2));
        Assert.Equal(CustomerInvoiceStatuses.Posted, inv.Status);
        Assert.Equal("FP-010.001-26.00000001", inv.TaxEvidenceReference);

        Assert.Throws<InvalidOperationException>(() => inv.Cancel(now.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() => inv.Submit(Guid.NewGuid(), now.AddMinutes(3)));
    }

    [Fact]
    public void CustomerReceivable_AppliesPartialAndFullCreditsWithoutGoingNegative()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var ar = CustomerReceivable.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "AR-2026-00001", Guid.NewGuid(), "INV-2026-00001",
            Guid.NewGuid(), "CUST-01", "PT Pelanggan",
            new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "IDR",
            666_000m, Guid.NewGuid(), "JRN-2026-11-00010", now);

        Assert.Equal(ReceivableStatuses.Open, ar.Status);
        Assert.Equal(666_000m, ar.OpenAmount);

        ar.ApplyCredit(166_500m, now.AddMinutes(1));
        Assert.Equal(ReceivableStatuses.PartiallySettled, ar.Status);
        Assert.Equal(166_500m, ar.CreditedAmount);
        Assert.Equal(499_500m, ar.OpenAmount);

        Assert.Throws<InvalidOperationException>(() => ar.ApplyCredit(500_000m, now.AddMinutes(2)));

        ar.ApplyCredit(499_500m, now.AddMinutes(3));
        Assert.Equal(ReceivableStatuses.Credited, ar.Status);
        Assert.Equal(0m, ar.OpenAmount);
    }
}
