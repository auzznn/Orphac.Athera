namespace Orphac.Athera.Api.Contracts;

public sealed record GlAccountDto(
    Guid AccountId,
    string AccountCode,
    string Name,
    string AccountType,
    string AccountCategory,
    string NormalBalance,
    Guid? ParentAccountId,
    bool AllowPosting,
    bool IsControlAccount,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record GlAccountListResponse(IReadOnlyList<GlAccountDto> Items);

public sealed record CreateGlAccountRequest(
    string AccountCode,
    string Name,
    string AccountType,
    string AccountCategory,
    Guid? ParentAccountId,
    bool AllowPosting,
    bool IsControlAccount);

public sealed record UpdateGlAccountRequest(
    string Name,
    string AccountCategory,
    Guid? ParentAccountId,
    bool AllowPosting,
    bool IsControlAccount,
    string? Status,
    long ExpectedVersion);

public sealed record FinancialPeriodDto(
    Guid PeriodId,
    string PeriodCode,
    int FiscalYear,
    int PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? CloseNote,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record FinancialPeriodListResponse(IReadOnlyList<FinancialPeriodDto> Items);

public sealed record CreateFinancialPeriodRequest(
    string PeriodCode,
    int FiscalYear,
    int PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CloseFinancialPeriodRequest(
    string? Note,
    bool HardLock);

public sealed record AccountMappingDto(
    Guid MappingId,
    string PurposeCode,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string? Notes,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record AccountMappingListResponse(
    IReadOnlyList<AccountMappingDto> Items,
    IReadOnlyList<string> RequiredPurposes);

public sealed record UpsertAccountMappingRequest(
    string PurposeCode,
    Guid AccountId,
    string? Notes);

public sealed record TaxCodeDto(
    Guid TaxCodeId,
    string Code,
    string Name,
    string TaxFamily,
    string Direction,
    string TreatmentClass,
    decimal NominalRatePercent,
    int TaxableBaseRatioNumerator,
    int TaxableBaseRatioDenominator,
    bool RequiresDocumentaryEvidence,
    bool RequiresCounterpartyNpwp,
    Guid? TaxAccountId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string ReviewStatus,
    string? StatutoryReferenceNote,
    string Status,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record TaxCodeListResponse(
    IReadOnlyList<TaxCodeDto> Items,
    string GovernanceNotice);

public sealed record CreateTaxCodeRequest(
    string Code,
    string Name,
    string TaxFamily,
    string Direction,
    string TreatmentClass,
    decimal NominalRatePercent,
    int TaxableBaseRatioNumerator,
    int TaxableBaseRatioDenominator,
    bool RequiresDocumentaryEvidence,
    bool RequiresCounterpartyNpwp,
    Guid? TaxAccountId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? StatutoryReferenceNote);

public sealed record EvaluateTaxPreviewRequest(
    Guid? TaxCodeId,
    decimal GrossAmount,
    DateOnly TransactionDate,
    bool HasCounterpartyNpwp,
    bool EvidenceProvided);

public sealed record EvaluateTaxPreviewResponse(
    Guid TaxCodeId,
    string Code,
    string TaxFamily,
    string Direction,
    string TreatmentClass,
    decimal GrossAmount,
    decimal TaxableBaseAmount,
    decimal NominalRatePercent,
    decimal EffectiveRatePercent,
    decimal TaxAmount,
    string ReviewStatus,
    string EvidenceStatus,
    bool IsBlocked,
    string? BlockedReason,
    string GovernanceDisclaimer);

public sealed record JournalLineInputDto(
    Guid AccountId,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    Guid? PartnerId,
    Guid? ItemId,
    Guid? TaxCodeId);

public sealed record PostJournalRequest(
    DateOnly PostingDate,
    string CurrencyCode,
    string Description,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceEventKey,
    IReadOnlyList<JournalLineInputDto> Lines);

public sealed record ReverseJournalRequest(
    DateOnly ReversalPostingDate,
    string Reason);

public sealed record JournalListItemDto(
    Guid JournalId,
    Guid PeriodId,
    string PeriodCode,
    string JournalNumber,
    string EntryKind,
    DateOnly PostingDate,
    string CurrencyCode,
    string Description,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceEventKey,
    Guid? ReversalOfJournalId,
    bool IsReversed,
    Guid? ReversedByJournalId,
    decimal TotalDebit,
    decimal TotalCredit,
    int LineCount,
    Guid PostedByUserId,
    DateTimeOffset PostedAtUtc);

public sealed record PagedJournalsResponse(
    IReadOnlyList<JournalListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record JournalLineDto(
    Guid LineId,
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    Guid? PartnerId,
    Guid? ItemId,
    Guid? TaxCodeId);

public sealed record JournalDetailDto(
    Guid JournalId,
    Guid PeriodId,
    string PeriodCode,
    string JournalNumber,
    string EntryKind,
    DateOnly PostingDate,
    string CurrencyCode,
    string Description,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceEventKey,
    string PayloadHash,
    Guid? ReversalOfJournalId,
    bool IsReversed,
    Guid? ReversedByJournalId,
    decimal TotalDebit,
    decimal TotalCredit,
    int LineCount,
    Guid PostedByUserId,
    DateTimeOffset PostedAtUtc,
    bool IdempotentReplay,
    IReadOnlyList<JournalLineDto> Lines);
