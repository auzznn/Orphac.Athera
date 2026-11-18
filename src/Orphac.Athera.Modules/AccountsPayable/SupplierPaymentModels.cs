using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.AccountsPayable;

public sealed class PaymentSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private PaymentSequence()
    {
    }
}

/// <summary>
/// Supplier settlement proposal and outbound clearing record (`ap` schema).
/// Stage 1 (`DRAFT`/`PENDING_RELEASE` + file simulation) has zero GL/AP impact;
/// Stage 2 (`RELEASED`) requires four-eyes authorization (`OD-02`) + explicit release reference (`OD-06`).
/// </summary>
public sealed class SupplierPayment
{
    public Guid SupplierPaymentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string DraftCode { get; private set; } = string.Empty;
    public string? PaymentNumber { get; private set; }
    public Guid BankAccountId { get; private set; }
    public string BankAccountCodeSnapshot { get; private set; } = string.Empty;
    public string BankNameSnapshot { get; private set; } = string.Empty;
    public Guid SupplierPartnerId { get; private set; }
    public string SupplierCodeSnapshot { get; private set; } = string.Empty;
    public string SupplierLegalNameSnapshot { get; private set; } = string.Empty;
    public string BeneficiaryBankName { get; private set; } = string.Empty;
    public string BeneficiaryAccountNumber { get; private set; } = string.Empty;
    public string BeneficiaryAccountHolder { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "IDR";
    public string PaymentMethod { get; private set; } = PaymentMethods.BankTransferIdr;
    public DateOnly PaymentDate { get; private set; }
    public decimal TotalAllocatedAmount { get; private set; }
    public decimal ReconciledAmount { get; private set; }
    public decimal RemainingClearingAmount { get; private set; }
    public string Status { get; private set; } = SupplierPaymentStatuses.Draft;
    public int SimulatedInstructionCount { get; private set; }
    public Guid? LatestInstructionId { get; private set; }
    public string? LatestInstructionCode { get; private set; }
    public string? ReleaseReference { get; private set; }
    public string? ReleaseIdempotencyKey { get; private set; }
    public Guid? ReleaseJournalId { get; private set; }
    public Guid? RejectionReversalJournalId { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? Notes { get; private set; }
    public Guid PreparedByUserId { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public Guid? ReleasedByUserId { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public Guid? RejectedByUserId { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private SupplierPayment()
    {
    }

    public static SupplierPayment CreateDraft(
        Guid paymentId,
        Guid tenantId,
        Guid legalEntityId,
        string draftCode,
        BankAccount bankAccount,
        Partner supplier,
        string beneficiaryBankName,
        string beneficiaryAccountNumber,
        string beneficiaryAccountHolder,
        string paymentMethod,
        DateOnly paymentDate,
        decimal totalAllocatedAmount,
        string? notes,
        Guid preparedByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(paymentId, nameof(paymentId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(preparedByUserId, nameof(preparedByUserId));
        ArgumentNullException.ThrowIfNull(bankAccount);
        ArgumentNullException.ThrowIfNull(supplier);

        var amt = decimal.Round(totalAllocatedAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAllocatedAmount), "Payment total must be strictly positive.");
        }

        return new SupplierPayment
        {
            SupplierPaymentId = paymentId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            DraftCode = GuardValidation.RequireCode(draftCode, nameof(draftCode), 40),
            BankAccountId = bankAccount.BankAccountId,
            BankAccountCodeSnapshot = bankAccount.AccountCode,
            BankNameSnapshot = bankAccount.BankName,
            SupplierPartnerId = supplier.PartnerId,
            SupplierCodeSnapshot = supplier.PartnerCode,
            SupplierLegalNameSnapshot = supplier.LegalName,
            BeneficiaryBankName = GuardValidation.RequireName(beneficiaryBankName, nameof(beneficiaryBankName), 160),
            BeneficiaryAccountNumber = GuardValidation.RequireName(beneficiaryAccountNumber, nameof(beneficiaryAccountNumber), 64),
            BeneficiaryAccountHolder = GuardValidation.RequireName(beneficiaryAccountHolder, nameof(beneficiaryAccountHolder), 160),
            CurrencyCode = bankAccount.CurrencyCode,
            PaymentMethod = PaymentMethods.Normalize(paymentMethod),
            PaymentDate = paymentDate,
            TotalAllocatedAmount = amt,
            ReconciledAmount = 0m,
            RemainingClearingAmount = 0m,
            Status = SupplierPaymentStatuses.Draft,
            SimulatedInstructionCount = 0,
            Notes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes)),
            PreparedByUserId = preparedByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDraft(
        BankAccount bankAccount,
        string beneficiaryBankName,
        string beneficiaryAccountNumber,
        string beneficiaryAccountHolder,
        string paymentMethod,
        DateOnly paymentDate,
        decimal totalAllocatedAmount,
        string? notes,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        if (Status != SupplierPaymentStatuses.Draft)
        {
            throw new InvalidOperationException($"payment_not_mutable: Supplier payment '{PaymentNumber ?? DraftCode}' is in status '{Status}' and cannot be edited.");
        }

        ArgumentNullException.ThrowIfNull(bankAccount);
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        var amt = decimal.Round(totalAllocatedAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAllocatedAmount), "Payment total must be strictly positive.");
        }

        BankAccountId = bankAccount.BankAccountId;
        BankAccountCodeSnapshot = bankAccount.AccountCode;
        BankNameSnapshot = bankAccount.BankName;
        BeneficiaryBankName = GuardValidation.RequireName(beneficiaryBankName, nameof(beneficiaryBankName), 160);
        BeneficiaryAccountNumber = GuardValidation.RequireName(beneficiaryAccountNumber, nameof(beneficiaryAccountNumber), 64);
        BeneficiaryAccountHolder = GuardValidation.RequireName(beneficiaryAccountHolder, nameof(beneficiaryAccountHolder), 160);
        PaymentMethod = PaymentMethods.Normalize(paymentMethod);
        PaymentDate = paymentDate;
        TotalAllocatedAmount = amt;
        Notes = GuardValidation.NormalizeOptional(notes, 400, nameof(notes));
        PreparedByUserId = actorUserId;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void RecordInstructionSimulation(Guid instructionId, string instructionNumber, DateTimeOffset nowUtc)
    {
        if (Status is not (SupplierPaymentStatuses.Draft or SupplierPaymentStatuses.PendingRelease))
        {
            throw new InvalidOperationException($"payment_cannot_simulate_file: Payment '{PaymentNumber ?? DraftCode}' is in status '{Status}'.");
        }

        GuardValidation.RequireNonEmptyGuid(instructionId, nameof(instructionId));
        SimulatedInstructionCount++;
        LatestInstructionId = instructionId;
        LatestInstructionCode = GuardValidation.RequireCode(instructionNumber, nameof(instructionNumber), 40);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void SubmitForRelease(Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != SupplierPaymentStatuses.Draft)
        {
            throw new InvalidOperationException($"payment_submit_invalid_state: Payment '{DraftCode}' is in status '{Status}' and must be DRAFT to submit.");
        }

        Status = SupplierPaymentStatuses.PendingRelease;
        SubmittedByUserId = actorUserId;
        SubmittedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ReleaseToClearing(
        string paymentNumber,
        string releaseReference,
        string releaseIdempotencyKey,
        Guid releaseJournalId,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        GuardValidation.RequireNonEmptyGuid(releaseJournalId, nameof(releaseJournalId));

        if (Status != SupplierPaymentStatuses.PendingRelease)
        {
            throw new InvalidOperationException($"payment_must_be_pending_release: Payment '{DraftCode}' is in status '{Status}' and must be submitted before four-eyes release.");
        }

        if (actorUserId == PreparedByUserId || (SubmittedByUserId.HasValue && actorUserId == SubmittedByUserId.Value))
        {
            throw new InvalidOperationException("four_eyes_violation: Payment release approver must differ from the preparer and submitter (OD-02).");
        }

        PaymentNumber = GuardValidation.RequireCode(paymentNumber, nameof(paymentNumber), 40);
        ReleaseReference = GuardValidation.RequireName(releaseReference, nameof(releaseReference), 120);
        ReleaseIdempotencyKey = GuardValidation.RequireName(releaseIdempotencyKey, nameof(releaseIdempotencyKey), 80);
        ReleaseJournalId = releaseJournalId;
        ReleasedByUserId = actorUserId;
        ReleasedAtUtc = nowUtc;
        ReconciledAmount = 0m;
        RemainingClearingAmount = TotalAllocatedAmount;
        Status = SupplierPaymentStatuses.Released;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ApplyBankReconciliation(decimal matchedAmount, DateTimeOffset nowUtc)
    {
        if (Status is not (SupplierPaymentStatuses.Released or SupplierPaymentStatuses.PartiallyReconciled))
        {
            throw new InvalidOperationException($"payment_not_in_outbound_clearing: Payment '{PaymentNumber ?? DraftCode}' is '{Status}'.");
        }

        var amt = decimal.Round(matchedAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(matchedAmount));
        }

        if (amt > RemainingClearingAmount)
        {
            throw new InvalidOperationException(
                $"match_exceeds_open_outbound_clearing: Match ({amt:F4}) exceeds open outbound clearing ({RemainingClearingAmount:F4}).");
        }

        ReconciledAmount = decimal.Round(ReconciledAmount + amt, 4, MidpointRounding.AwayFromZero);
        RemainingClearingAmount = decimal.Round(TotalAllocatedAmount - ReconciledAmount, 4, MidpointRounding.AwayFromZero);
        if (RemainingClearingAmount < 0m || TotalAllocatedAmount != ReconciledAmount + RemainingClearingAmount)
        {
            throw new InvalidOperationException("payment_clearing_conservation_failed");
        }

        Status = RemainingClearingAmount == 0m
            ? SupplierPaymentStatuses.Reconciled
            : SupplierPaymentStatuses.PartiallyReconciled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ReverseBankReconciliation(decimal reversalAmount, DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(reversalAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m || amt > ReconciledAmount)
        {
            throw new InvalidOperationException("reversal_exceeds_reconciled_amount");
        }

        ReconciledAmount = decimal.Round(ReconciledAmount - amt, 4, MidpointRounding.AwayFromZero);
        RemainingClearingAmount = decimal.Round(TotalAllocatedAmount - ReconciledAmount, 4, MidpointRounding.AwayFromZero);
        Status = ReconciledAmount == 0m
            ? SupplierPaymentStatuses.Released
            : SupplierPaymentStatuses.PartiallyReconciled;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void MarkBankRejected(Guid reversalJournalId, string reason, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(reversalJournalId, nameof(reversalJournalId));
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != SupplierPaymentStatuses.Released || ReconciledAmount != 0m)
        {
            throw new InvalidOperationException(
                "payment_already_reconciled_reverse_match_first: Only unreconciled RELEASED payments can be bank-rejected directly; reverse bank matches first.");
        }

        Status = SupplierPaymentStatuses.BankRejected;
        RemainingClearingAmount = 0m;
        RejectionReversalJournalId = reversalJournalId;
        RejectionReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        RejectedByUserId = actorUserId;
        RejectedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class PaymentAllocation
{
    public Guid PaymentAllocationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid SupplierPaymentId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid PayableItemId { get; private set; }
    public string PayableNumberSnapshot { get; private set; } = string.Empty;
    public Guid SupplierBillId { get; private set; }
    public string SupplierInvoiceNumberSnapshot { get; private set; } = string.Empty;
    public decimal PayableOpenBeforeAmount { get; private set; }
    public decimal AllocatedAmount { get; private set; }
    public decimal PayableRemainingAfterAmount { get; private set; }
    public string? Notes { get; private set; }

    private PaymentAllocation()
    {
    }

    public static PaymentAllocation Create(
        Guid allocationId,
        Guid tenantId,
        Guid legalEntityId,
        Guid supplierPaymentId,
        int lineNumber,
        PayableItem payable,
        decimal allocatedAmount,
        string? notes)
    {
        GuardValidation.RequireNonEmptyGuid(allocationId, nameof(allocationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierPaymentId, nameof(supplierPaymentId));
        ArgumentNullException.ThrowIfNull(payable);
        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        }

        var amt = decimal.Round(allocatedAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(allocatedAmount), "Allocated amount must be strictly positive.");
        }

        if (amt > payable.RemainingOpenAmount)
        {
            throw new InvalidOperationException(
                $"allocation_exceeds_open_payable: Allocated amount ({amt:F4}) exceeds open payable balance ({payable.RemainingOpenAmount:F4}).");
        }

        return new PaymentAllocation
        {
            PaymentAllocationId = allocationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SupplierPaymentId = supplierPaymentId,
            LineNumber = lineNumber,
            PayableItemId = payable.PayableItemId,
            PayableNumberSnapshot = payable.PayableNumber,
            SupplierBillId = payable.SupplierBillId,
            SupplierInvoiceNumberSnapshot = payable.SupplierInvoiceNumber,
            PayableOpenBeforeAmount = payable.RemainingOpenAmount,
            AllocatedAmount = amt,
            PayableRemainingAfterAmount = decimal.Round(payable.RemainingOpenAmount - amt, 4, MidpointRounding.AwayFromZero),
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes))
        };
    }

    public void RefreshSettlementSnapshots(decimal openBefore, decimal remainingAfter)
    {
        PayableOpenBeforeAmount = decimal.Round(openBefore, 4, MidpointRounding.AwayFromZero);
        PayableRemainingAfterAmount = decimal.Round(remainingAfter, 4, MidpointRounding.AwayFromZero);
    }
}
