namespace Orphac.Athera.Api.Contracts;

public sealed record ReceivablesBankAccountDto(
    Guid BankAccountId,
    string AccountCode,
    string BankName,
    string AccountNumberMasked,
    string CurrencyCode);

public sealed record InboundBankCreditLineDto(
    Guid BankStatementLineId,
    Guid BankStatementId,
    Guid BankAccountId,
    string BankAccountCode,
    DateOnly ValueDate,
    decimal Amount,
    decimal MatchedAmount,
    decimal UnmatchedAmount,
    string BankReference,
    string? CounterpartyName,
    string Description,
    string Status);

public sealed record ReceivablesCustomerOptionDto(
    Guid PartnerId,
    string PartnerCode,
    string LegalName);

public sealed record ReceiptAllocationDto(
    Guid AllocationId,
    Guid ReceiptId,
    Guid ReceivableId,
    Guid InvoiceId,
    string InvoiceNumber,
    decimal AllocatedAmount,
    string Status,
    DateTimeOffset AllocatedAtUtc,
    DateTimeOffset? ReversedAtUtc);

public sealed record CustomerReceiptDto(
    Guid ReceiptId,
    string ReceiptNumber,
    Guid BankAccountId,
    Guid BankStatementLineId,
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    DateOnly ReceiptDate,
    string BankReference,
    string CurrencyCode,
    decimal ReceiptAmount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    string Status,
    Guid GlJournalId,
    string GlJournalNumber,
    string? ReversalGlJournalNumber,
    string? ReversalReason,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReversedAtUtc,
    IReadOnlyList<ReceiptAllocationDto> Allocations);

public sealed record ArAgingCustomerRowDto(
    Guid CustomerPartnerId,
    string CustomerCode,
    string CustomerName,
    decimal CurrentAmount,
    decimal Days1To30Amount,
    decimal Days31To60Amount,
    decimal Days61To90Amount,
    decimal Over90Amount,
    decimal TotalOpenReceivables,
    decimal UnappliedReceiptsAmount,
    decimal NetReceivableBalance);

public sealed record ArAgingSummaryDto(
    DateOnly AsOfDate,
    decimal CurrentTotal,
    decimal Days1To30Total,
    decimal Days31To60Total,
    decimal Days61To90Total,
    decimal Over90Total,
    decimal TotalOpenReceivables,
    decimal TotalUnappliedReceipts,
    decimal NetArSubledger,
    decimal GlArControlBalance,
    bool IsGlReconciled,
    IReadOnlyList<ArAgingCustomerRowDto> Customers);

public sealed record ReceivablesOverviewResponse(
    bool CanManage,
    ArAgingSummaryDto Aging,
    IReadOnlyList<ReceivablesBankAccountDto> BankAccounts,
    IReadOnlyList<InboundBankCreditLineDto> InboundBankLines,
    IReadOnlyList<ReceivablesCustomerOptionDto> Customers,
    IReadOnlyList<CustomerReceivableDto> OpenReceivables,
    IReadOnlyList<CustomerReceiptDto> Receipts);

public sealed record ImportInboundBankStatementRequest(
    Guid BankAccountId,
    string StatementReference,
    DateOnly StatementDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string CsvContent);

public sealed record ImportInboundBankStatementResponse(
    Guid BankStatementId,
    string StatementNumber,
    int ImportedCount,
    int DuplicateSkippedCount);

public sealed record ReceiptAllocationLineRequest(
    Guid ReceivableId,
    decimal AllocatedAmount);

public sealed record ConfirmCustomerReceiptRequest(
    Guid BankStatementLineId,
    Guid CustomerPartnerId,
    DateOnly ReceiptDate,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<ReceiptAllocationLineRequest>? InitialAllocations);

public sealed record AllocateCustomerReceiptRequest(
    IReadOnlyList<ReceiptAllocationLineRequest> Allocations);

public sealed record ReverseCustomerReceiptRequest(
    DateOnly ReversalDate,
    string Reason);
