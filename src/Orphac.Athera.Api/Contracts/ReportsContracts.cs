namespace Orphac.Athera.Api.Contracts;

public sealed record TrialBalanceRowDto(
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

public sealed record TrialBalanceReportDto(
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
    IReadOnlyList<TrialBalanceRowDto> Rows);

public sealed record StatementAccountLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal Amount);

public sealed record ProfitAndLossReportDto(
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal NetIncome,
    IReadOnlyList<StatementAccountLineDto> RevenueLines,
    IReadOnlyList<StatementAccountLineDto> ExpenseLines);

public sealed record BalanceSheetReportDto(
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal CurrentEarnings,
    decimal TotalLiabilitiesAndEquity,
    bool IsBalanced,
    IReadOnlyList<StatementAccountLineDto> AssetLines,
    IReadOnlyList<StatementAccountLineDto> LiabilityLines,
    IReadOnlyList<StatementAccountLineDto> EquityLines);

public sealed record ControlReconciliationRowDto(
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

public sealed record AgingBucketTotalsDto(
    decimal CurrentAmount,
    decimal Days1To30Amount,
    decimal Days31To60Amount,
    decimal Days61To90Amount,
    decimal Over90Amount,
    decimal TotalOpenAmount,
    int OpenItemCount);

public sealed record ReportPeriodDto(
    Guid PeriodId,
    string PeriodCode,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    string? CloseNote,
    DateTimeOffset? ClosedAtUtc);

public sealed record PeriodCloseReviewDto(
    Guid CloseReviewId,
    Guid PeriodId,
    string PeriodCode,
    string ActionType,
    decimal TrialBalanceDebits,
    decimal TrialBalanceCredits,
    bool IsTrialBalanceBalanced,
    int ReconciliationVarianceCount,
    decimal TotalVarianceAmount,
    decimal UnbilledGrniAmount,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record AccountingReportsOverviewResponse(
    bool CanClosePeriod,
    bool CanLockOrReopen,
    TrialBalanceReportDto TrialBalance,
    ProfitAndLossReportDto ProfitAndLoss,
    BalanceSheetReportDto BalanceSheet,
    IReadOnlyList<ControlReconciliationRowDto> Reconciliations,
    AgingBucketTotalsDto ApAging,
    AgingBucketTotalsDto ArAging,
    IReadOnlyList<ReportPeriodDto> Periods,
    IReadOnlyList<PeriodCloseReviewDto> CloseReviews);

public sealed record AccountLedgerEntryDto(
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

public sealed record AccountLedgerDrilldownResponse(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string NormalBalance,
    IReadOnlyList<AccountLedgerEntryDto> Entries);

public sealed record ClosePeriodRequest(
    string? Note,
    bool BlockOnVariance = true);

public sealed record LockPeriodRequest(
    string? Note);

public sealed record ReopenPeriodRequest(
    string Reason);
