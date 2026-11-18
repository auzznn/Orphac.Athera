namespace Orphac.Athera.Api.Contracts;

public sealed record UpsertBankAccountRequest(
    string AccountCode,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string AccountHolderName,
    Guid GlAccountId,
    Guid? ClearingGlAccountId,
    string Status);

public sealed record PaymentAllocationInputRequest(
    Guid PayableItemId,
    decimal AllocatedAmount,
    string? Notes);

public sealed record CreateSupplierPaymentProposalRequest(
    Guid BankAccountId,
    Guid SupplierPartnerId,
    string BeneficiaryBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryAccountHolder,
    string PaymentMethod,
    DateOnly PaymentDate,
    string? Notes,
    IReadOnlyList<PaymentAllocationInputRequest> Allocations);

public sealed record UpdateSupplierPaymentProposalRequest(
    Guid BankAccountId,
    string BeneficiaryBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryAccountHolder,
    string PaymentMethod,
    DateOnly PaymentDate,
    string? Notes,
    IReadOnlyList<PaymentAllocationInputRequest> Allocations);

public sealed record SimulatePaymentInstructionRequest(
    string? FormatCode);

public sealed record ReleaseSupplierPaymentRequest(
    DateOnly PostingDate,
    string ReleaseReference,
    string IdempotencyKey);

public sealed record RejectReleasedPaymentRequest(
    DateOnly ReversalPostingDate,
    string Reason);

public sealed record BankStatementLineImportRequest(
    DateOnly ValueDate,
    string Direction,
    decimal Amount,
    string BankReference,
    string? CounterpartyName,
    string Description);

public sealed record ImportBankStatementRequest(
    Guid BankAccountId,
    string StatementReference,
    DateOnly StatementDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? CsvContent,
    IReadOnlyList<BankStatementLineImportRequest>? Lines);

public sealed record ReconcileBankStatementLineRequest(
    Guid BankStatementLineId,
    Guid SupplierPaymentId,
    DateOnly PostingDate,
    decimal? MatchedAmount,
    string IdempotencyKey);

public sealed record ReverseBankMatchRequest(
    DateOnly ReversalPostingDate,
    string Reason);

public sealed record BankAccountDto(
    Guid BankAccountId,
    string AccountCode,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string AccountHolderName,
    string CurrencyCode,
    Guid GlAccountId,
    string GlAccountCode,
    string GlAccountName,
    Guid ClearingGlAccountId,
    string ClearingGlAccountCode,
    string ClearingGlAccountName,
    string Status);

public sealed record PaymentAllocationDto(
    Guid PaymentAllocationId,
    int LineNumber,
    Guid PayableItemId,
    string PayableNumber,
    Guid SupplierBillId,
    string SupplierInvoiceNumber,
    decimal PayableOpenBeforeAmount,
    decimal AllocatedAmount,
    decimal PayableRemainingAfterAmount,
    string? Notes);

public sealed record PaymentInstructionDto(
    Guid PaymentInstructionId,
    string InstructionNumber,
    string FormatCode,
    bool SimulationOnly,
    decimal TotalAmount,
    string CurrencyCode,
    string PayloadText,
    string PayloadSha256,
    string? GeneratedByEmail,
    DateTimeOffset GeneratedAtUtc);

public sealed record SupplierPaymentSummaryDto(
    Guid SupplierPaymentId,
    string DraftCode,
    string? PaymentNumber,
    Guid BankAccountId,
    string BankAccountCode,
    string BankName,
    Guid SupplierPartnerId,
    string SupplierCode,
    string SupplierLegalName,
    string BeneficiaryBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryAccountHolder,
    string CurrencyCode,
    string PaymentMethod,
    DateOnly PaymentDate,
    decimal TotalAllocatedAmount,
    decimal ReconciledAmount,
    decimal RemainingClearingAmount,
    string Status,
    int SimulatedInstructionCount,
    Guid? LatestInstructionId,
    string? LatestInstructionCode,
    string? ReleaseReference,
    Guid? ReleaseJournalId,
    string? ReleaseJournalNumber,
    Guid? RejectionReversalJournalId,
    string? RejectionReversalJournalNumber,
    string? RejectionReason,
    string? Notes,
    string? PreparedByEmail,
    string? SubmittedByEmail,
    string? ReleasedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record BankStatementDto(
    Guid BankStatementId,
    string StatementNumber,
    Guid BankAccountId,
    string BankAccountCode,
    string StatementReference,
    DateOnly StatementDate,
    string CurrencyCode,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebitAmount,
    decimal TotalCreditAmount,
    int LineCount,
    string SourceFormat,
    string? ImportedByEmail,
    DateTimeOffset ImportedAtUtc);

public sealed record BankStatementLineDto(
    Guid BankStatementLineId,
    Guid BankStatementId,
    string StatementNumber,
    Guid BankAccountId,
    string BankAccountCode,
    int LineNumber,
    DateOnly ValueDate,
    string Direction,
    decimal Amount,
    decimal MatchedAmount,
    decimal UnmatchedAmount,
    string BankReference,
    string? CounterpartyName,
    string Description,
    string Status);

public sealed record BankPaymentMatchDto(
    Guid BankPaymentMatchId,
    string MatchNumber,
    Guid BankAccountId,
    string BankAccountCode,
    Guid BankStatementId,
    string StatementNumber,
    Guid BankStatementLineId,
    string BankReference,
    Guid SupplierPaymentId,
    string PaymentDisplayNumber,
    DateOnly PostingDate,
    decimal MatchedAmount,
    string Status,
    Guid ClearingGlJournalId,
    string ClearingGlJournalNumber,
    Guid? ReversalGlJournalId,
    string? ReversalGlJournalNumber,
    string? ReversalReason,
    string? MatchedByEmail,
    DateTimeOffset MatchedAtUtc);

public sealed record ClearingReconciliationProofDto(
    decimal GlOutboundClearingCreditNetBalance,
    decimal SubledgerOpenClearingBalance,
    decimal DiscrepancyAmount,
    bool IsReconciled,
    decimal GlMainOperatingBankNetBalance);

public sealed record PaymentsGlAccountOptionDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountCategory);

public sealed record PaymentsOverviewResponse(
    string Od06PolicyNotice,
    IReadOnlyList<BankAccountDto> BankAccounts,
    IReadOnlyList<PaymentsGlAccountOptionDto> CashAndClearingAccounts,
    IReadOnlyList<PayableItemDto> OpenPayables,
    IReadOnlyList<SupplierPaymentSummaryDto> Payments,
    IReadOnlyList<BankStatementDto> BankStatements,
    IReadOnlyList<BankStatementLineDto> StatementLines,
    IReadOnlyList<BankPaymentMatchDto> Matches,
    ClearingReconciliationProofDto ClearingProof,
    DateTimeOffset ServerTimestampUtc);

public sealed record SupplierPaymentDetailResponse(
    SupplierPaymentSummaryDto Payment,
    IReadOnlyList<PaymentAllocationDto> Allocations,
    IReadOnlyList<PaymentInstructionDto> Instructions,
    IReadOnlyList<BankPaymentMatchDto> Matches,
    bool IdempotentReplay);
