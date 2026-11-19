using Orphac.Athera.Modules.GeneralLedger;

namespace Orphac.Athera.UnitTests;

public sealed class S16ReportsUnitTests
{
    [Fact]
    public void FinancialPeriod_ReopenFromClosedSucceedsAndLockedPeriodBlocksReopen()
    {
        var now = new DateTimeOffset(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        var actor = Guid.NewGuid();
        var period = FinancialPeriod.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            FinancialPeriodStatuses.Open, now);

        period.Close(actor, "Month-end close", now.AddMinutes(1));
        Assert.Equal(FinancialPeriodStatuses.Closed, period.Status);

        period.ReopenFromClosed(actor, "Late adjustment approved", now.AddMinutes(2));
        Assert.Equal(FinancialPeriodStatuses.Open, period.Status);

        period.Close(actor, "Re-close", now.AddMinutes(3));
        period.Lock(actor, "Permanent lock", now.AddMinutes(4));
        Assert.Equal(FinancialPeriodStatuses.Locked, period.Status);

        var ex = Assert.Throws<InvalidOperationException>(() => period.ReopenFromClosed(actor, "Try unlock", now.AddMinutes(5)));
        Assert.Contains("period_locked_reopen_blocked", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrialBalanceCsvExporter_MatchesReportTotalsExactly()
    {
        var tb = new TrialBalanceReport(
            Guid.NewGuid(),
            "2026-11",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            0m, 0m, 500_000m, 500_000m, 500_000m, 500_000m, true,
            [
                new TrialBalanceRow(Guid.NewGuid(), "1110", "Kas & Bank", GlAccountTypes.Asset, GlNormalBalances.Debit, 0m, 0m, 500_000m, 0m, 500_000m, 0m),
                new TrialBalanceRow(Guid.NewGuid(), "3100", "Modal Disetor", GlAccountTypes.Equity, GlNormalBalances.Credit, 0m, 0m, 0m, 500_000m, 0m, 500_000m)
            ]);

        var csv = AccountingReportsService.BuildTrialBalanceCsv(tb);
        Assert.Contains("1110,\"Kas & Bank\",ASSET,0.00,0.00,500000.00,0.00,500000.00,0.00", csv);
        Assert.Contains("TOTAL,\"Total Neraca Saldo (2026-11)\",SUMMARY,0.00,0.00,500000.00,500000.00,500000.00,500000.00", csv);
    }
}
