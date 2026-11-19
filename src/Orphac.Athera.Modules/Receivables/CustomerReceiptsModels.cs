using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Receivables;

public static class CustomerReceiptStatuses
{
    public const string Confirmed = "CONFIRMED";
    public const string PartiallyAllocated = "PARTIALLY_ALLOCATED";
    public const string Allocated = "ALLOCATED";
    public const string Reversed = "REVERSED";

    public static readonly IReadOnlyList<string> All = [Confirmed, PartiallyAllocated, Allocated, Reversed];
}

public static class ReceiptAllocationStatuses
{
    public const string Active = "ACTIVE";
    public const string Reversed = "REVERSED";

    public static readonly IReadOnlyList<string> All = [Active, Reversed];
}

public sealed class CustomerReceiptSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private CustomerReceiptSequence() { }
}

public sealed class CustomerReceipt
{
    public Guid ReceiptId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public Guid BankAccountId { get; private set; }
    public Guid BankStatementId { get; private set; }
    public Guid BankStatementLineId { get; private set; }
    public Guid CustomerPartnerId { get; private set; }
    public string CustomerCodeSnapshot { get; private set; } = string.Empty;
    public string CustomerNameSnapshot { get; private set; } = string.Empty;
    public DateOnly ReceiptDate { get; private set; }
    public string BankReferenceSnapshot { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal ReceiptAmount { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public decimal UnappliedAmount { get; private set; }
    public string Status { get; private set; } = CustomerReceiptStatuses.Confirmed;
    public Guid GlJournalId { get; private set; }
    public string GlJournalNumber { get; private set; } = string.Empty;
    public Guid? ReversalGlJournalId { get; private set; }
    public string? ReversalGlJournalNumber { get; private set; }
    public string? ReversalReason { get; private set; }
    public Guid? ReversedByUserId { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private CustomerReceipt() { }

    public static CustomerReceipt CreateConfirmed(
        Guid receiptId,
        Guid tenantId,
        Guid legalEntityId,
        string receiptNumber,
        Guid bankAccountId,
        Guid bankStatementId,
        Guid bankStatementLineId,
        Guid customerPartnerId,
        string customerCodeSnapshot,
        string customerNameSnapshot,
        DateOnly receiptDate,
        string bankReferenceSnapshot,
        string currencyCode,
        decimal receiptAmount,
        Guid glJournalId,
        string glJournalNumber,
        string idempotencyKey,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(receiptAmount, 2, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(receiptAmount), "ar_receipt_amount_must_be_positive: Receipt amount must be strictly positive.");
        }

        return new CustomerReceipt
        {
            ReceiptId = GuardValidation.RequireNonEmptyGuid(receiptId, nameof(receiptId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            ReceiptNumber = GuardValidation.RequireCode(receiptNumber, nameof(receiptNumber), 40),
            BankAccountId = GuardValidation.RequireNonEmptyGuid(bankAccountId, nameof(bankAccountId)),
            BankStatementId = GuardValidation.RequireNonEmptyGuid(bankStatementId, nameof(bankStatementId)),
            BankStatementLineId = GuardValidation.RequireNonEmptyGuid(bankStatementLineId, nameof(bankStatementLineId)),
            CustomerPartnerId = GuardValidation.RequireNonEmptyGuid(customerPartnerId, nameof(customerPartnerId)),
            CustomerCodeSnapshot = GuardValidation.RequireCode(customerCodeSnapshot, nameof(customerCodeSnapshot), 32),
            CustomerNameSnapshot = GuardValidation.RequireName(customerNameSnapshot, nameof(customerNameSnapshot), 200),
            ReceiptDate = receiptDate,
            BankReferenceSnapshot = GuardValidation.RequireName(bankReferenceSnapshot, nameof(bankReferenceSnapshot), 100),
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            ReceiptAmount = amt,
            AllocatedAmount = 0m,
            UnappliedAmount = amt,
            Status = CustomerReceiptStatuses.Confirmed,
            GlJournalId = GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId)),
            GlJournalNumber = GuardValidation.RequireCode(glJournalNumber, nameof(glJournalNumber), 40),
            IdempotencyKey = GuardValidation.RequireName(idempotencyKey, nameof(idempotencyKey), 80),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            CreatedByUserId = GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyAllocation(decimal allocationAmount, DateTimeOffset nowUtc)
    {
        if (Status == CustomerReceiptStatuses.Reversed)
        {
            throw new InvalidOperationException("ar_receipt_already_reversed: Reversed receipts cannot be allocated.");
        }

        var amt = decimal.Round(allocationAmount, 2, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocationAmount), "ar_allocation_must_be_positive: Allocation amount must be strictly positive.");
        }

        if (amt > UnappliedAmount)
        {
            throw new InvalidOperationException(
                $"ar_allocation_exceeds_unapplied: Allocation ({amt:0.00}) exceeds unapplied receipt balance ({UnappliedAmount:0.00}).");
        }

        AllocatedAmount = decimal.Round(AllocatedAmount + amt, 2, MidpointRounding.AwayFromZero);
        UnappliedAmount = decimal.Round(ReceiptAmount - AllocatedAmount, 2, MidpointRounding.AwayFromZero);
        Status = UnappliedAmount == 0m
            ? CustomerReceiptStatuses.Allocated
            : CustomerReceiptStatuses.PartiallyAllocated;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkReversed(
        Guid reversalGlJournalId,
        string reversalGlJournalNumber,
        string reason,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        if (Status == CustomerReceiptStatuses.Reversed)
        {
            throw new InvalidOperationException("ar_receipt_already_reversed: Receipt has already been reversed.");
        }

        ReversalGlJournalId = GuardValidation.RequireNonEmptyGuid(reversalGlJournalId, nameof(reversalGlJournalId));
        ReversalGlJournalNumber = GuardValidation.RequireCode(reversalGlJournalNumber, nameof(reversalGlJournalNumber), 40);
        ReversalReason = GuardValidation.RequireName(reason, nameof(reason), 240);
        ReversedByUserId = GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        ReversedAtUtc = nowUtc;
        AllocatedAmount = 0m;
        UnappliedAmount = ReceiptAmount;
        Status = CustomerReceiptStatuses.Reversed;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class ReceiptAllocation
{
    public Guid AllocationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ReceiptId { get; private set; }
    public Guid ReceivableId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string InvoiceNumberSnapshot { get; private set; } = string.Empty;
    public decimal AllocatedAmount { get; private set; }
    public string Status { get; private set; } = ReceiptAllocationStatuses.Active;
    public Guid AllocatedByUserId { get; private set; }
    public DateTimeOffset AllocatedAtUtc { get; private set; }
    public Guid? ReversedByUserId { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }

    private ReceiptAllocation() { }

    public static ReceiptAllocation Create(
        Guid allocationId,
        Guid tenantId,
        Guid legalEntityId,
        Guid receiptId,
        Guid receivableId,
        Guid invoiceId,
        string invoiceNumberSnapshot,
        decimal allocatedAmount,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(allocatedAmount, 2, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedAmount), "ar_allocation_must_be_positive: Allocation amount must be strictly positive.");
        }

        return new ReceiptAllocation
        {
            AllocationId = GuardValidation.RequireNonEmptyGuid(allocationId, nameof(allocationId)),
            TenantId = GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId)),
            LegalEntityId = GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId)),
            ReceiptId = GuardValidation.RequireNonEmptyGuid(receiptId, nameof(receiptId)),
            ReceivableId = GuardValidation.RequireNonEmptyGuid(receivableId, nameof(receivableId)),
            InvoiceId = GuardValidation.RequireNonEmptyGuid(invoiceId, nameof(invoiceId)),
            InvoiceNumberSnapshot = GuardValidation.RequireCode(invoiceNumberSnapshot, nameof(invoiceNumberSnapshot), 40),
            AllocatedAmount = amt,
            Status = ReceiptAllocationStatuses.Active,
            AllocatedByUserId = GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId)),
            AllocatedAtUtc = nowUtc
        };
    }

    public void MarkReversed(Guid actorUserId, DateTimeOffset nowUtc)
    {
        if (Status == ReceiptAllocationStatuses.Reversed) return;
        Status = ReceiptAllocationStatuses.Reversed;
        ReversedByUserId = GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        ReversedAtUtc = nowUtc;
    }
}

public static class ArAgingBuckets
{
    public const string Current = "CURRENT";
    public const string Days1To30 = "DAYS_1_30";
    public const string Days31To60 = "DAYS_31_60";
    public const string Days61To90 = "DAYS_61_90";
    public const string Over90 = "OVER_90";

    public static (string Bucket, int DaysPastDue) Classify(DateOnly dueDate, DateOnly asOfDate)
    {
        var daysPastDue = Math.Max(0, asOfDate.DayNumber - dueDate.DayNumber);
        var bucket = daysPastDue switch
        {
            0 => Current,
            <= 30 => Days1To30,
            <= 60 => Days31To60,
            <= 90 => Days61To90,
            _ => Over90
        };
        return (bucket, daysPastDue);
    }
}
