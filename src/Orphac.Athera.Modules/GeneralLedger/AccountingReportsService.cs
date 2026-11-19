using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Receivables;

namespace Orphac.Athera.Modules.GeneralLedger;

public sealed record TrialBalanceRow(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string NormalBalance,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal EndingDebit,
    decimal EndingCredit);

public sealed record TrialBalanceReport(
    Guid? PeriodId,
    string PeriodCode,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalOpeningDebit,
    decimal TotalOpeningCredit,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    decimal TotalEndingDebit,
    decimal TotalEndingCredit,
    bool IsBalanced,
    IReadOnlyList<TrialBalanceRow> Rows);

public sealed record StatementAccountLine(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal Amount);

public sealed record ProfitAndLossReport(
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal NetIncome,
    IReadOnlyList<StatementAccountLine> RevenueLines,
    IReadOnlyList<StatementAccountLine> ExpenseLines);

public sealed record BalanceSheetReport(
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal CurrentEarnings,
    decimal TotalLiabilitiesAndEquity,
    bool IsBalanced,
    IReadOnlyList<StatementAccountLine> AssetLines,
    IReadOnlyList<StatementAccountLine> LiabilityLines,
    IReadOnlyList<StatementAccountLine> EquityLines);

public sealed record ControlReconciliationRow(
    string PurposeCode,
    string ControlLabel,
    string? AccountCode,
    string? AccountName,
    Guid? AccountId,
    decimal GlBalance,
    decimal SubledgerBalance,
    decimal VarianceAmount,
    string Status,
    int OpenItemCount,
    string Explanation);

public sealed record AgingBucketTotals(
    decimal CurrentAmount,
    decimal Days1To30Amount,
    decimal Days31To60Amount,
    decimal Days61To90Amount,
    decimal Over90Amount,
    decimal TotalOpenAmount,
    int OpenItemCount);

public sealed record AccountLedgerEntryRow(
    Guid JournalId,
    string JournalNumber,
    DateOnly PostingDate,
    string EntryKind,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string Description,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal RunningBalance);

public sealed record AccountingReportsSnapshot(
    TrialBalanceReport TrialBalance,
    ProfitAndLossReport ProfitAndLoss,
    BalanceSheetReport BalanceSheet,
    IReadOnlyList<ControlReconciliationRow> Reconciliations,
    AgingBucketTotals ApAging,
    AgingBucketTotals ArAging,
    IReadOnlyList<FinancialPeriod> Periods,
    IReadOnlyList<PeriodCloseReview> CloseReviews);

public sealed record PeriodCloseOutcome(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? Title,
    string? Detail,
    FinancialPeriod? Period,
    PeriodCloseReview? Review)
{
    public static PeriodCloseOutcome Ok(FinancialPeriod period, PeriodCloseReview review) =>
        new(true, 200, null, null, null, period, review);

    public static PeriodCloseOutcome Fail(int statusCode, string errorCode, string title, string detail) =>
        new(false, statusCode, errorCode, title, detail, null, null);
}

public static class AccountingReportsService
{
    public static async Task<AccountingReportsSnapshot> BuildSnapshotAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid? requestedPeriodId,
        DateOnly asOfDate,
        CancellationToken ct)
    {
        var periods = await db.FinancialPeriods.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == legalEntityId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(ct);

        var selectedPeriod = requestedPeriodId.HasValue
            ? periods.FirstOrDefault(p => p.PeriodId == requestedPeriodId.Value)
            : periods.FirstOrDefault(p => p.StartDate <= asOfDate && p.EndDate >= asOfDate) ?? periods.FirstOrDefault();

        var startDate = selectedPeriod?.StartDate ?? new DateOnly(asOfDate.Year, asOfDate.Month, 1);
        var endDate = selectedPeriod?.EndDate ?? asOfDate;
        var periodCode = selectedPeriod?.PeriodCode ?? $"{asOfDate:yyyy-MM}";

        var accounts = await db.GlAccounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.LegalEntityId == legalEntityId && a.AllowPosting)
            .OrderBy(a => a.AccountCode)
            .ToListAsync(ct);

        var journalLines = await (
            from jl in db.JournalLines.AsNoTracking()
            join je in db.JournalEntries.AsNoTracking()
                on new { jl.TenantId, jl.LegalEntityId, jl.JournalId } equals new { je.TenantId, je.LegalEntityId, je.JournalId }
            where jl.TenantId == tenantId
                && jl.LegalEntityId == legalEntityId
                && je.PostingDate <= endDate
            select new { jl.AccountId, je.PostingDate, jl.DebitAmount, jl.CreditAmount })
            .ToListAsync(ct);

        var tbRows = new List<TrialBalanceRow>();
        decimal totOpenDr = 0m, totOpenCr = 0m, totPerDr = 0m, totPerCr = 0m, totEndDr = 0m, totEndCr = 0m;
        var endingNetByAccountId = new Dictionary<Guid, decimal>();
        var periodNetByAccountId = new Dictionary<Guid, decimal>();

        foreach (var acct in accounts)
        {
            var acctLines = journalLines.Where(x => x.AccountId == acct.AccountId).ToList();
            var openDrRaw = acctLines.Where(x => x.PostingDate < startDate).Sum(x => x.DebitAmount);
            var openCrRaw = acctLines.Where(x => x.PostingDate < startDate).Sum(x => x.CreditAmount);
            var openNet = decimal.Round(openDrRaw - openCrRaw, 2, MidpointRounding.AwayFromZero);
            var openDr = openNet > 0m ? openNet : 0m;
            var openCr = openNet < 0m ? -openNet : 0m;

            var perDr = decimal.Round(acctLines.Where(x => x.PostingDate >= startDate && x.PostingDate <= endDate).Sum(x => x.DebitAmount), 2, MidpointRounding.AwayFromZero);
            var perCr = decimal.Round(acctLines.Where(x => x.PostingDate >= startDate && x.PostingDate <= endDate).Sum(x => x.CreditAmount), 2, MidpointRounding.AwayFromZero);

            var endNet = decimal.Round(openNet + perDr - perCr, 2, MidpointRounding.AwayFromZero);
            var endDr = endNet > 0m ? endNet : 0m;
            var endCr = endNet < 0m ? -endNet : 0m;

            endingNetByAccountId[acct.AccountId] = endNet;
            periodNetByAccountId[acct.AccountId] = decimal.Round(perDr - perCr, 2, MidpointRounding.AwayFromZero);

            if (openDr == 0m && openCr == 0m && perDr == 0m && perCr == 0m)
            {
                continue;
            }

            tbRows.Add(new TrialBalanceRow(
                acct.AccountId, acct.AccountCode, acct.Name, acct.AccountType, acct.NormalBalance,
                openDr, openCr, perDr, perCr, endDr, endCr));

            totOpenDr += openDr;
            totOpenCr += openCr;
            totPerDr += perDr;
            totPerCr += perCr;
            totEndDr += endDr;
            totEndCr += endCr;
        }

        var tb = new TrialBalanceReport(
            selectedPeriod?.PeriodId,
            periodCode,
            startDate,
            endDate,
            decimal.Round(totOpenDr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totOpenCr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totPerDr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totPerCr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totEndDr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totEndCr, 2, MidpointRounding.AwayFromZero),
            decimal.Round(totEndDr, 2, MidpointRounding.AwayFromZero) == decimal.Round(totEndCr, 2, MidpointRounding.AwayFromZero),
            tbRows);

        // Profit & Loss (Cumulative through endDate for Balance Sheet consistency, Revenue credit-normal, Expense debit-normal)
        var revLines = accounts
            .Where(a => a.AccountType == GlAccountTypes.Revenue)
            .Select(a => new StatementAccountLine(a.AccountId, a.AccountCode, a.Name, a.AccountType, decimal.Round(-endingNetByAccountId.GetValueOrDefault(a.AccountId, 0m), 2, MidpointRounding.AwayFromZero)))
            .Where(x => x.Amount != 0m)
            .ToList();
        var expLines = accounts
            .Where(a => a.AccountType == GlAccountTypes.Expense)
            .Select(a => new StatementAccountLine(a.AccountId, a.AccountCode, a.Name, a.AccountType, decimal.Round(endingNetByAccountId.GetValueOrDefault(a.AccountId, 0m), 2, MidpointRounding.AwayFromZero)))
            .Where(x => x.Amount != 0m)
            .ToList();
        var totRev = decimal.Round(revLines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var totExp = decimal.Round(expLines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var netIncome = decimal.Round(totRev - totExp, 2, MidpointRounding.AwayFromZero);
        var pnl = new ProfitAndLossReport(totRev, totExp, netIncome, revLines, expLines);

        // Balance Sheet (Assets = Liabilities + Equity + CurrentEarnings)
        var assetLines = accounts
            .Where(a => a.AccountType == GlAccountTypes.Asset)
            .Select(a => new StatementAccountLine(a.AccountId, a.AccountCode, a.Name, a.AccountType, decimal.Round(endingNetByAccountId.GetValueOrDefault(a.AccountId, 0m), 2, MidpointRounding.AwayFromZero)))
            .Where(x => x.Amount != 0m)
            .ToList();
        var liabLines = accounts
            .Where(a => a.AccountType == GlAccountTypes.Liability)
            .Select(a => new StatementAccountLine(a.AccountId, a.AccountCode, a.Name, a.AccountType, decimal.Round(-endingNetByAccountId.GetValueOrDefault(a.AccountId, 0m), 2, MidpointRounding.AwayFromZero)))
            .Where(x => x.Amount != 0m)
            .ToList();
        var eqLines = accounts
            .Where(a => a.AccountType == GlAccountTypes.Equity)
            .Select(a => new StatementAccountLine(a.AccountId, a.AccountCode, a.Name, a.AccountType, decimal.Round(-endingNetByAccountId.GetValueOrDefault(a.AccountId, 0m), 2, MidpointRounding.AwayFromZero)))
            .Where(x => x.Amount != 0m)
            .ToList();

        var totAssets = decimal.Round(assetLines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var totLiab = decimal.Round(liabLines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var totEq = decimal.Round(eqLines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var totLiabEq = decimal.Round(totLiab + totEq + netIncome, 2, MidpointRounding.AwayFromZero);
        var bs = new BalanceSheetReport(totAssets, totLiab, totEq, netIncome, totLiabEq, totAssets == totLiabEq, assetLines, liabLines, eqLines);

        // 5-Control Reconciliations (as of current authoritative state across all posted journals)
        var allGlTotals = await (
            from jl in db.JournalLines.AsNoTracking()
            join je in db.JournalEntries.AsNoTracking()
                on new { jl.TenantId, jl.LegalEntityId, jl.JournalId } equals new { je.TenantId, je.LegalEntityId, je.JournalId }
            where jl.TenantId == tenantId && jl.LegalEntityId == legalEntityId
            group jl by jl.AccountId into g
            select new { AccountId = g.Key, NetDebit = g.Sum(x => x.DebitAmount - x.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, x => decimal.Round(x.NetDebit, 2, MidpointRounding.AwayFromZero), ct);

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.LegalEntityId == legalEntityId)
            .ToDictionaryAsync(m => m.PurposeCode, m => m.AccountId, StringComparer.Ordinal, ct);
        var accountById = accounts.ToDictionary(a => a.AccountId);

        // Subledger totals
        var openClearingPayments = await db.SupplierPayments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == legalEntityId && p.RemainingClearingAmount > 0m)
            .Select(p => p.RemainingClearingAmount)
            .ToListAsync(ct);
        var subClearing = decimal.Round(openClearingPayments.Sum(), 2, MidpointRounding.AwayFromZero);

        var openArRows = await db.CustomerReceivables.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == legalEntityId && r.OpenAmount > 0m)
            .Select(r => new { r.DueDate, r.OpenAmount })
            .ToListAsync(ct);
        var unappliedReceipts = await db.CustomerReceipts.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == legalEntityId && r.Status != CustomerReceiptStatuses.Reversed && r.UnappliedAmount > 0m)
            .Select(r => r.UnappliedAmount)
            .ToListAsync(ct);
        var subArNet = decimal.Round(openArRows.Sum(x => x.OpenAmount) - unappliedReceipts.Sum(), 2, MidpointRounding.AwayFromZero);

        var invPools = await db.ValuationPools.AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.LegalEntityId == legalEntityId)
            .Select(v => v.CarryingValueAmount)
            .ToListAsync(ct);
        var subInventory = decimal.Round(invPools.Sum(), 2, MidpointRounding.AwayFromZero);

        var openGrniLines = await db.ReceiptAccrualLines.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.LegalEntityId == legalEntityId && l.RemainingOpenLineAmount > 0m)
            .Select(l => l.RemainingOpenLineAmount)
            .ToListAsync(ct);
        var subGrni = decimal.Round(openGrniLines.Sum(), 2, MidpointRounding.AwayFromZero);

        var openApRows = await db.PayableItems.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == legalEntityId && p.RemainingOpenAmount > 0m)
            .Select(p => new { p.DueDate, p.RemainingOpenAmount })
            .ToListAsync(ct);
        var subAp = decimal.Round(openApRows.Sum(x => x.RemainingOpenAmount), 2, MidpointRounding.AwayFromZero);

        ControlReconciliationRow BuildRecon(string purpose, string label, bool creditNormal, decimal subBal, int openCount, string okNote)
        {
            if (!mappings.TryGetValue(purpose, out var acctId) || !accountById.TryGetValue(acctId, out var acct))
            {
                return new ControlReconciliationRow(purpose, label, null, null, null, 0m, subBal, -subBal, "UNMAPPED", openCount, "Akun kontrol GL belum dipetakan.");
            }

            var rawNetDr = allGlTotals.GetValueOrDefault(acctId, 0m);
            var glBal = creditNormal ? -rawNetDr : rawNetDr;
            var variance = decimal.Round(glBal - subBal, 2, MidpointRounding.AwayFromZero);
            var status = variance == 0m ? "RECONCILED" : "VARIANCE";
            var exp = status == "RECONCILED" ? $"GL {acct.AccountCode} {okNote}" : $"Selisih GL vs Subledger sebesar {variance:N2} IDR perlu ditelusuri.";
            return new ControlReconciliationRow(purpose, label, acct.AccountCode, acct.Name, acctId, glBal, subBal, variance, status, openCount, exp);
        }

        var reconciliations = new List<ControlReconciliationRow>
        {
            BuildRecon(GlMappingPurposes.BankOutboundClearing, "Kliring Pembayaran Keluar", true, subClearing, openClearingPayments.Count, "seimbang dengan pembayaran keluar belum terekonsiliasi bank."),
            BuildRecon(GlMappingPurposes.ArTradeControl, "Kontrol Piutang Usaha (AR)", false, subArNet, openArRows.Count, "seimbang dengan Piutang Terbuka dikurangi Penerimaan Belum Alokasi."),
            BuildRecon(GlMappingPurposes.InventoryAsset, "Persediaan Barang Dagang", false, subInventory, invPools.Count, "seimbang dengan total nilai tercatat rata-rata tertimbang."),
            BuildRecon(GlMappingPurposes.GrniClearing, "Akrual GRNI Belum Difaktur", true, subGrni, openGrniLines.Count, "seimbang dengan sisa baris penerimaan bernilai yang belum ditagih pemasok."),
            BuildRecon(GlMappingPurposes.ApTradeControl, "Kontrol Hutang Usaha (AP)", true, subAp, openApRows.Count, "seimbang dengan total saldo terbuka kartu Hutang Usaha (AP).")
        };

        var apAging = ComputeAging(openApRows.Select(x => (x.DueDate, x.RemainingOpenAmount)), asOfDate);
        var arAging = ComputeAging(openArRows.Select(x => (x.DueDate, x.OpenAmount)), asOfDate);

        var closeReviews = await db.PeriodCloseReviews.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.LegalEntityId == legalEntityId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(30)
            .ToListAsync(ct);

        return new AccountingReportsSnapshot(tb, pnl, bs, reconciliations, apAging, arAging, periods, closeReviews);
    }

    public static async Task<IReadOnlyList<AccountLedgerEntryRow>> GetAccountLedgerAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct)
    {
        var acct = await db.GlAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.LegalEntityId == legalEntityId && a.AccountId == accountId, ct);
        if (acct is null) return [];

        var query =
            from jl in db.JournalLines.AsNoTracking()
            join je in db.JournalEntries.AsNoTracking()
                on new { jl.TenantId, jl.LegalEntityId, jl.JournalId } equals new { je.TenantId, je.LegalEntityId, je.JournalId }
            where jl.TenantId == tenantId
                && jl.LegalEntityId == legalEntityId
                && jl.AccountId == accountId
            select new
            {
                je.JournalId,
                je.JournalNumber,
                je.PostingDate,
                je.EntryKind,
                je.SourceModule,
                je.SourceDocumentType,
                je.SourceDocumentId,
                Description = jl.Description ?? je.Description,
                jl.DebitAmount,
                jl.CreditAmount,
                je.PostedAtUtc,
                jl.LineNumber
            };

        if (fromDate.HasValue) query = query.Where(x => x.PostingDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.PostingDate <= toDate.Value);

        var rows = await query
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.PostedAtUtc)
            .ThenBy(x => x.LineNumber)
            .ToListAsync(ct);

        var creditNormal = acct.NormalBalance == GlNormalBalances.Credit;
        decimal running = 0m;
        var result = new List<AccountLedgerEntryRow>(rows.Count);
        foreach (var r in rows)
        {
            var delta = creditNormal ? (r.CreditAmount - r.DebitAmount) : (r.DebitAmount - r.CreditAmount);
            running = decimal.Round(running + delta, 2, MidpointRounding.AwayFromZero);
            result.Add(new AccountLedgerEntryRow(
                r.JournalId, r.JournalNumber, r.PostingDate, r.EntryKind,
                r.SourceModule, r.SourceDocumentType, r.SourceDocumentId, r.Description,
                r.DebitAmount, r.CreditAmount, running));
        }

        return result;
    }

    public static string BuildTrialBalanceCsv(TrialBalanceReport tb)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AccountCode,AccountName,AccountType,OpeningDebit,OpeningCredit,PeriodDebit,PeriodCredit,EndingDebit,EndingCredit");
        foreach (var r in tb.Rows)
        {
            var safeName = r.AccountName.Replace("\"", "\"\"", StringComparison.Ordinal);
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{r.AccountCode},\"{safeName}\",{r.AccountType},{r.OpeningDebit:F2},{r.OpeningCredit:F2},{r.PeriodDebit:F2},{r.PeriodCredit:F2},{r.EndingDebit:F2},{r.EndingCredit:F2}"));
        }

        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"TOTAL,\"Total Neraca Saldo ({tb.PeriodCode})\",SUMMARY,{tb.TotalOpeningDebit:F2},{tb.TotalOpeningCredit:F2},{tb.TotalPeriodDebit:F2},{tb.TotalPeriodCredit:F2},{tb.TotalEndingDebit:F2},{tb.TotalEndingCredit:F2}"));
        return sb.ToString();
    }

    public static async Task<PeriodCloseOutcome> ClosePeriodAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid periodId,
        string? note,
        bool blockOnVariance,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var period = await LockPeriodAsync(db, tenantId, legalEntityId, periodId, ct);
        if (period is null)
        {
            return PeriodCloseOutcome.Fail(404, "period_not_found", "Financial period not found", "The requested financial period does not exist in this entity.");
        }

        if (period.Status != FinancialPeriodStatuses.Open)
        {
            return PeriodCloseOutcome.Fail(409, "period_not_open", "Period is not open", $"Financial period '{period.PeriodCode}' is currently '{period.Status}'.");
        }

        var snap = await BuildSnapshotAsync(db, tenantId, legalEntityId, periodId, period.EndDate, ct);
        if (!snap.TrialBalance.IsBalanced)
        {
            return PeriodCloseOutcome.Fail(409, "trial_balance_unbalanced", "Trial balance is not balanced", "Cannot close a financial period while total debits do not equal total credits.");
        }

        var variances = snap.Reconciliations.Where(r => r.Status != "RECONCILED").ToList();
        var totalVar = decimal.Round(variances.Sum(v => Math.Abs(v.VarianceAmount)), 2, MidpointRounding.AwayFromZero);
        if (blockOnVariance && variances.Count > 0)
        {
            return PeriodCloseOutcome.Fail(
                409, "period_close_control_variance", "Control account variance blocks close",
                $"Found {variances.Count} control variance(s) totaling {totalVar:N2} IDR. Resolve variances before closing.");
        }

        var grniRow = snap.Reconciliations.FirstOrDefault(r => r.PurposeCode == GlMappingPurposes.GrniClearing);
        period.Close(actorUserId, note, nowUtc);

        var review = PeriodCloseReview.Create(
            Guid.NewGuid(), tenantId, legalEntityId, period.PeriodId, period.PeriodCode,
            PeriodCloseActionTypes.Closed,
            snap.TrialBalance.TotalEndingDebit, snap.TrialBalance.TotalEndingCredit,
            variances.Count, totalVar, grniRow?.SubledgerBalance ?? 0m,
            JsonSerializer.Serialize(new { snap.Reconciliations }),
            note, actorUserId, nowUtc);

        db.PeriodCloseReviews.Add(review);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(), tenantId, legalEntityId, actorUserId,
            "GL_PERIOD_CLOSED", "FinancialPeriod", period.PeriodId.ToString("D"),
            $"Closed financial period {period.PeriodCode} (TB {snap.TrialBalance.TotalEndingDebit:0.00})",
            JsonSerializer.Serialize(new { period.PeriodCode, review.CloseReviewId, variances = variances.Count }),
            nowUtc));
        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(), tenantId, legalEntityId,
            "gl.period.closed", review.CloseReviewId.ToString("D"),
            JsonSerializer.Serialize(new { period.PeriodId, period.PeriodCode, review.CloseReviewId }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return PeriodCloseOutcome.Ok(period, review);
    }

    public static async Task<PeriodCloseOutcome> LockPeriodPermanentlyAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid periodId,
        string? note,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var period = await LockPeriodAsync(db, tenantId, legalEntityId, periodId, ct);
        if (period is null)
        {
            return PeriodCloseOutcome.Fail(404, "period_not_found", "Financial period not found", "The requested financial period does not exist in this entity.");
        }

        if (period.Status != FinancialPeriodStatuses.Closed)
        {
            return PeriodCloseOutcome.Fail(409, "period_must_be_closed_before_lock", "Period must be closed first", $"Only CLOSED periods can be permanently locked (current: '{period.Status}').");
        }

        var snap = await BuildSnapshotAsync(db, tenantId, legalEntityId, periodId, period.EndDate, ct);
        var variances = snap.Reconciliations.Where(r => r.Status != "RECONCILED").ToList();
        var grniRow = snap.Reconciliations.FirstOrDefault(r => r.PurposeCode == GlMappingPurposes.GrniClearing);

        period.Lock(actorUserId, note, nowUtc);
        var review = PeriodCloseReview.Create(
            Guid.NewGuid(), tenantId, legalEntityId, period.PeriodId, period.PeriodCode,
            PeriodCloseActionTypes.Locked,
            snap.TrialBalance.TotalEndingDebit, snap.TrialBalance.TotalEndingCredit,
            variances.Count, decimal.Round(variances.Sum(v => Math.Abs(v.VarianceAmount)), 2, MidpointRounding.AwayFromZero),
            grniRow?.SubledgerBalance ?? 0m,
            JsonSerializer.Serialize(new { locked = true }),
            note, actorUserId, nowUtc);

        db.PeriodCloseReviews.Add(review);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(), tenantId, legalEntityId, actorUserId,
            "GL_PERIOD_LOCKED", "FinancialPeriod", period.PeriodId.ToString("D"),
            $"Permanently locked financial period {period.PeriodCode}",
            JsonSerializer.Serialize(new { period.PeriodCode, review.CloseReviewId }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return PeriodCloseOutcome.Ok(period, review);
    }

    public static async Task<PeriodCloseOutcome> ReopenPeriodAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid periodId,
        string reason,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return PeriodCloseOutcome.Fail(400, "period_reopen_reason_required", "Reopen reason required", "A written justification is mandatory to reopen a closed financial period.");
        }

        var period = await LockPeriodAsync(db, tenantId, legalEntityId, periodId, ct);
        if (period is null)
        {
            return PeriodCloseOutcome.Fail(404, "period_not_found", "Financial period not found", "The requested financial period does not exist in this entity.");
        }

        try
        {
            period.ReopenFromClosed(actorUserId, reason, nowUtc);
        }
        catch (InvalidOperationException ex)
        {
            var code = ex.Message.StartsWith("period_locked_reopen_blocked", StringComparison.Ordinal)
                ? "period_locked_reopen_blocked"
                : "period_not_closed";
            return PeriodCloseOutcome.Fail(409, code, "Cannot reopen period", ex.Message);
        }

        var snap = await BuildSnapshotAsync(db, tenantId, legalEntityId, periodId, period.EndDate, ct);
        var grniRow = snap.Reconciliations.FirstOrDefault(r => r.PurposeCode == GlMappingPurposes.GrniClearing);
        var review = PeriodCloseReview.Create(
            Guid.NewGuid(), tenantId, legalEntityId, period.PeriodId, period.PeriodCode,
            PeriodCloseActionTypes.Reopened,
            snap.TrialBalance.TotalEndingDebit, snap.TrialBalance.TotalEndingCredit,
            snap.Reconciliations.Count(r => r.Status != "RECONCILED"),
            decimal.Round(snap.Reconciliations.Where(r => r.Status != "RECONCILED").Sum(v => Math.Abs(v.VarianceAmount)), 2, MidpointRounding.AwayFromZero),
            grniRow?.SubledgerBalance ?? 0m,
            JsonSerializer.Serialize(new { reopened = true, reason = reason.Trim() }),
            reason, actorUserId, nowUtc);

        db.PeriodCloseReviews.Add(review);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(), tenantId, legalEntityId, actorUserId,
            "GL_PERIOD_REOPENED", "FinancialPeriod", period.PeriodId.ToString("D"),
            $"Reopened closed financial period {period.PeriodCode}: {reason.Trim()}",
            JsonSerializer.Serialize(new { period.PeriodCode, review.CloseReviewId, reason = reason.Trim() }),
            nowUtc));

        await db.SaveChangesAsync(ct);
        return PeriodCloseOutcome.Ok(period, review);
    }

    private static Task<FinancialPeriod?> LockPeriodAsync(AtheraDbContext db, Guid tenantId, Guid legalEntityId, Guid periodId, CancellationToken ct) =>
        db.FinancialPeriods
            .FromSqlInterpolated($"""
                SELECT * FROM gl.financial_periods
                WHERE tenant_id = {tenantId}
                  AND legal_entity_id = {legalEntityId}
                  AND period_id = {periodId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

    private static AgingBucketTotals ComputeAging(IEnumerable<(DateOnly DueDate, decimal OpenAmount)> items, DateOnly asOfDate)
    {
        decimal c0 = 0m, c1 = 0m, c2 = 0m, c3 = 0m, c4 = 0m;
        int count = 0;
        foreach (var (dueDate, openAmt) in items)
        {
            if (openAmt <= 0m) continue;
            count++;
            var (bucket, _) = ArAgingBuckets.Classify(dueDate, asOfDate);
            switch (bucket)
            {
                case ArAgingBuckets.Current: c0 += openAmt; break;
                case ArAgingBuckets.Days1To30: c1 += openAmt; break;
                case ArAgingBuckets.Days31To60: c2 += openAmt; break;
                case ArAgingBuckets.Days61To90: c3 += openAmt; break;
                default: c4 += openAmt; break;
            }
        }

        return new AgingBucketTotals(
            decimal.Round(c0, 2, MidpointRounding.AwayFromZero),
            decimal.Round(c1, 2, MidpointRounding.AwayFromZero),
            decimal.Round(c2, 2, MidpointRounding.AwayFromZero),
            decimal.Round(c3, 2, MidpointRounding.AwayFromZero),
            decimal.Round(c4, 2, MidpointRounding.AwayFromZero),
            decimal.Round(c0 + c1 + c2 + c3 + c4, 2, MidpointRounding.AwayFromZero),
            count);
    }
}
