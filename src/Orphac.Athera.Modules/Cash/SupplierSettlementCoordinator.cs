using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Cash;

public sealed record UpsertBankAccountCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    string AccountCode,
    string BankName,
    string? BranchName,
    string AccountNumber,
    string AccountHolderName,
    Guid GlAccountId,
    Guid? ClearingGlAccountId,
    string Status,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record BankAccountOutcome(
    bool Succeeded,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    BankAccount? BankAccount)
{
    public static BankAccountOutcome Fail(int status, string code, string detail) => new(false, status, code, detail, null);
    public static BankAccountOutcome Success(BankAccount bank) => new(true, 200, null, null, bank);
}

public sealed record PaymentAllocationInput(
    Guid PayableItemId,
    decimal AllocatedAmount,
    string? Notes = null);

public sealed record CreatePaymentProposalCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid BankAccountId,
    Guid SupplierPartnerId,
    string BeneficiaryBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryAccountHolder,
    string PaymentMethod,
    DateOnly PaymentDate,
    string? Notes,
    IReadOnlyList<PaymentAllocationInput> Allocations,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record UpdatePaymentProposalCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierPaymentId,
    string ExpectedFunctionalCurrencyCode,
    Guid BankAccountId,
    string BeneficiaryBankName,
    string BeneficiaryAccountNumber,
    string BeneficiaryAccountHolder,
    string PaymentMethod,
    DateOnly PaymentDate,
    string? Notes,
    IReadOnlyList<PaymentAllocationInput> Allocations,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SimulatePaymentFileCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierPaymentId,
    string FormatCode,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SubmitPaymentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierPaymentId,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReleasePaymentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierPaymentId,
    string ExpectedFunctionalCurrencyCode,
    DateOnly PostingDate,
    string ReleaseReference,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc,
    Func<Task>? OnAfterGlPostedBeforeCommitAsync = null);

public sealed record RejectReleasedPaymentCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid SupplierPaymentId,
    string ExpectedFunctionalCurrencyCode,
    DateOnly ReversalPostingDate,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record SupplierPaymentOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    SupplierPayment? Payment,
    IReadOnlyList<PaymentAllocation> Allocations,
    PaymentInstruction? LatestInstruction,
    JournalEntry? Journal)
{
    public static SupplierPaymentOutcome Fail(int status, string code, string detail) =>
        new(false, false, status, code, detail, null, Array.Empty<PaymentAllocation>(), null, null);

    public static SupplierPaymentOutcome Success(
        SupplierPayment payment,
        IReadOnlyList<PaymentAllocation> allocations,
        PaymentInstruction? instruction = null,
        JournalEntry? journal = null,
        bool replay = false) =>
        new(true, replay, replay ? 200 : 201, null, null, payment, allocations, instruction, journal);
}

public static class SupplierSettlementCoordinator
{
    public static async Task<BankAccountOutcome> UpsertBankAccountAsync(
        AtheraDbContext db,
        UpsertBankAccountCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var code = GuardValidation.RequireMasterCode(cmd.AccountCode, nameof(cmd.AccountCode), 2, 32);
            var bankGl = await db.GlAccounts.AsNoTracking().SingleOrDefaultAsync(
                a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.AccountId == cmd.GlAccountId,
                ct);
            if (bankGl is null || !bankGl.AllowPosting || bankGl.Status != MasterStatuses.Active ||
                bankGl.AccountCategory != GlAccountCategories.CashBank)
            {
                return BankAccountOutcome.Fail(
                    400,
                    "invalid_bank_gl_account",
                    "Bank operating GL account must be an active postable CASH_BANK asset account in this legal entity.");
            }
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.BankMainOperating, bankGl, cmd.LegalEntityId);

            GlAccount? clearingGl = null;
            if (cmd.ClearingGlAccountId is { } cid && cid != Guid.Empty)
            {
                clearingGl = await db.GlAccounts.AsNoTracking().SingleOrDefaultAsync(
                    a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.AccountId == cid,
                    ct);
            }
            else
            {
                var clearingMapping = await db.AccountMappings.AsNoTracking().SingleOrDefaultAsync(
                    m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId && m.PurposeCode == GlMappingPurposes.BankOutboundClearing,
                    ct);
                if (clearingMapping is not null)
                {
                    clearingGl = await db.GlAccounts.AsNoTracking().SingleOrDefaultAsync(
                        a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.AccountId == clearingMapping.AccountId,
                        ct);
                }
            }

            if (clearingGl is null || !clearingGl.AllowPosting || clearingGl.Status != MasterStatuses.Active ||
                clearingGl.AccountCategory != GlAccountCategories.BankOutboundClearing)
            {
                return BankAccountOutcome.Fail(
                    400,
                    "invalid_clearing_gl_account",
                    "Outbound clearing GL account must be an active postable BANK_OUTBOUND_CLEARING account in this legal entity.");
            }
            AccountMappingValidator.ValidateAccountForPurpose(GlMappingPurposes.BankOutboundClearing, clearingGl, cmd.LegalEntityId);

            var existing = await db.BankAccounts.SingleOrDefaultAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.AccountCode == code,
                ct);
            if (existing is null)
            {
                existing = BankAccount.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    code,
                    cmd.BankName,
                    cmd.BranchName,
                    cmd.AccountNumber,
                    cmd.AccountHolderName,
                    cmd.ExpectedFunctionalCurrencyCode,
                    bankGl.AccountId,
                    clearingGl.AccountId,
                    cmd.ActorUserId,
                    cmd.NowUtc);
                db.BankAccounts.Add(existing);
            }
            else
            {
                existing.UpdateDetails(
                    cmd.BankName,
                    cmd.BranchName,
                    cmd.AccountNumber,
                    cmd.AccountHolderName,
                    bankGl.AccountId,
                    clearingGl.AccountId,
                    cmd.Status,
                    cmd.NowUtc);
            }

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_BANK_ACCOUNT_SAVED",
                "BANK_ACCOUNT",
                existing.BankAccountId.ToString("D"),
                $"Saved bank account {existing.AccountCode} ({existing.BankName}) linked to GL {bankGl.AccountCode}/{clearingGl.AccountCode}",
                JsonSerializer.Serialize(new { existing.BankAccountId, existing.AccountCode, BankGl = bankGl.AccountCode, ClearingGl = clearingGl.AccountCode, existing.Status }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return BankAccountOutcome.Success(existing);
        }
        catch (ArgumentException ex)
        {
            return BankAccountOutcome.Fail(400, "validation_error", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return BankAccountOutcome.Fail(status, code, detail);
        }
    }

    public static async Task<SupplierPaymentOutcome> CreatePaymentProposalAsync(
        AtheraDbContext db,
        CreatePaymentProposalCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("CreatePaymentProposalAsync requires active entity transaction.");
        const string sp = "sp_create_supplier_payment";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            if (cmd.Allocations is null || cmd.Allocations.Count == 0)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "allocations_required", "At least one open payable allocation is required.");
            }

            var bankAccount = await db.BankAccounts.SingleOrDefaultAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == cmd.BankAccountId,
                ct);
            if (bankAccount is null || bankAccount.Status != MasterStatuses.Active)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "invalid_bank_account", "Active bank account was not found in this legal entity.");
            }

            var supplier = await db.Partners.AsNoTracking().SingleOrDefaultAsync(
                p => p.TenantId == cmd.TenantId && p.PartnerId == cmd.SupplierPartnerId && p.IsSupplier && p.Status == PartnerStatuses.Active,
                ct);
            if (supplier is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "invalid_supplier", "Active eligible supplier was not found.");
            }

            var payableIds = cmd.Allocations.Select(a => a.PayableItemId).ToList();
            if (payableIds.Distinct().Count() != payableIds.Count)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "duplicate_payable_in_proposal", "Each payable item may appear at most once per payment proposal.");
            }

            var lockedPayables = await CashCoordinatorHelpers.LockPayablesOrderedAsync(db, cmd.TenantId, cmd.LegalEntityId, payableIds, ct);
            if (lockedPayables.Count != payableIds.Count)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payable_not_found", "One or more payables do not belong to this legal entity.");
            }

            var payableMap = lockedPayables.ToDictionary(p => p.PayableItemId);
            var validationError = await ValidateProposalPayablesAsync(db, cmd.TenantId, cmd.LegalEntityId, supplier.PartnerId, bankAccount.CurrencyCode, null, cmd.Allocations, payableMap, ct);
            if (validationError is not null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return validationError;
            }

            var totalAllocated = cmd.Allocations.Sum(a => decimal.Round(a.AllocatedAmount, 4, MidpointRounding.AwayFromZero));
            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "ap.payment_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PaymentDate.Year, ct);
            var draftCode = $"PAY-DRF-{cmd.PaymentDate.Year:D4}-{seq:D5}";

            var payment = SupplierPayment.CreateDraft(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                draftCode,
                bankAccount,
                supplier,
                cmd.BeneficiaryBankName,
                cmd.BeneficiaryAccountNumber,
                cmd.BeneficiaryAccountHolder,
                cmd.PaymentMethod,
                cmd.PaymentDate,
                totalAllocated,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);
            db.SupplierPayments.Add(payment);

            var allocations = new List<PaymentAllocation>(cmd.Allocations.Count);
            for (var i = 0; i < cmd.Allocations.Count; i++)
            {
                var input = cmd.Allocations[i];
                var alloc = PaymentAllocation.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    payment.SupplierPaymentId,
                    i + 1,
                    payableMap[input.PayableItemId],
                    input.AllocatedAmount,
                    input.Notes);
                allocations.Add(alloc);
                db.PaymentAllocations.Add(alloc);
            }

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_PAYMENT_PROPOSAL_CREATED",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Prepared payment proposal {payment.DraftCode} for {supplier.PartnerCode} totaling {totalAllocated:N0} {payment.CurrencyCode} across {allocations.Count} payable(s)",
                JsonSerializer.Serialize(new { payment.SupplierPaymentId, payment.DraftCode, totalAllocated, Count = allocations.Count }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Success(payment, allocations);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Fail(400, "payment_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
    }

    public static async Task<SupplierPaymentOutcome> UpdatePaymentProposalAsync(
        AtheraDbContext db,
        UpdatePaymentProposalCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("UpdatePaymentProposalAsync requires active entity transaction.");
        const string sp = "sp_update_supplier_payment";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            if (cmd.Allocations is null || cmd.Allocations.Count == 0)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "allocations_required", "At least one open payable allocation is required.");
            }

            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payment_not_found", "Supplier payment proposal was not found.");
            }

            if (payment.Status != SupplierPaymentStatuses.Draft)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(409, "payment_not_mutable", $"Payment '{payment.PaymentNumber ?? payment.DraftCode}' is in status '{payment.Status}'.");
            }

            var bankAccount = await db.BankAccounts.SingleOrDefaultAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == cmd.BankAccountId,
                ct);
            if (bankAccount is null || bankAccount.Status != MasterStatuses.Active)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "invalid_bank_account", "Active bank account was not found in this legal entity.");
            }

            var payableIds = cmd.Allocations.Select(a => a.PayableItemId).ToList();
            if (payableIds.Distinct().Count() != payableIds.Count)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "duplicate_payable_in_proposal", "Each payable item may appear at most once.");
            }

            var lockedPayables = await CashCoordinatorHelpers.LockPayablesOrderedAsync(db, cmd.TenantId, cmd.LegalEntityId, payableIds, ct);
            if (lockedPayables.Count != payableIds.Count)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payable_not_found", "One or more payables do not belong to this legal entity.");
            }

            var payableMap = lockedPayables.ToDictionary(p => p.PayableItemId);
            var validationError = await ValidateProposalPayablesAsync(
                db, cmd.TenantId, cmd.LegalEntityId, payment.SupplierPartnerId, bankAccount.CurrencyCode, payment.SupplierPaymentId, cmd.Allocations, payableMap, ct);
            if (validationError is not null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return validationError;
            }

            var totalAllocated = cmd.Allocations.Sum(a => decimal.Round(a.AllocatedAmount, 4, MidpointRounding.AwayFromZero));
            payment.UpdateDraft(
                bankAccount,
                cmd.BeneficiaryBankName,
                cmd.BeneficiaryAccountNumber,
                cmd.BeneficiaryAccountHolder,
                cmd.PaymentMethod,
                cmd.PaymentDate,
                totalAllocated,
                cmd.Notes,
                cmd.ActorUserId,
                cmd.NowUtc);

            var existingAllocations = await db.PaymentAllocations
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SupplierPaymentId == payment.SupplierPaymentId)
                .ToListAsync(ct);
            db.PaymentAllocations.RemoveRange(existingAllocations);
            await db.SaveChangesAsync(ct);

            var newAllocations = new List<PaymentAllocation>(cmd.Allocations.Count);
            for (var i = 0; i < cmd.Allocations.Count; i++)
            {
                var input = cmd.Allocations[i];
                var alloc = PaymentAllocation.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    payment.SupplierPaymentId,
                    i + 1,
                    payableMap[input.PayableItemId],
                    input.AllocatedAmount,
                    input.Notes);
                newAllocations.Add(alloc);
                db.PaymentAllocations.Add(alloc);
            }

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_PAYMENT_PROPOSAL_UPDATED",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Updated payment proposal {payment.DraftCode} to {totalAllocated:N0} {payment.CurrencyCode}",
                JsonSerializer.Serialize(new { payment.SupplierPaymentId, payment.DraftCode, totalAllocated }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Success(payment, newAllocations);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Fail(400, "payment_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
    }

    public static async Task<SupplierPaymentOutcome> SimulatePaymentInstructionFileAsync(
        AtheraDbContext db,
        SimulatePaymentFileCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                return SupplierPaymentOutcome.Fail(404, "payment_not_found", "Supplier payment was not found.");
            }

            var bankAccount = await db.BankAccounts.AsNoTracking().SingleAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == payment.BankAccountId,
                ct);
            var allocations = await db.PaymentAllocations.AsNoTracking()
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SupplierPaymentId == payment.SupplierPaymentId)
                .OrderBy(a => a.LineNumber)
                .ToListAsync(ct);

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "cash.instruction_sequences", cmd.TenantId, cmd.LegalEntityId, payment.PaymentDate.Year, ct);
            var instructionNumber = $"PIN-{payment.PaymentDate.Year:D4}-{seq:D5}";
            var format = PaymentInstructionFormats.Normalize(cmd.FormatCode);

            var sb = new StringBuilder();
            sb.AppendLine("# SIMULATED_NON_LIVE_PAYMENT_INSTRUCTION | OD-06 SAFE DEFAULT | ZERO GL & ZERO AP REDUCTION");
            sb.AppendLine("InstructionNo,PaymentDraftCode,BankAccountCode,BankAccountNumber,SupplierCode,SupplierName,BeneficiaryBank,BeneficiaryAccount,BeneficiaryHolder,Method,PaymentDate,PayableNumber,SupplierInvoiceNumber,Currency,Amount");
            foreach (var a in allocations)
            {
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{instructionNumber},{payment.DraftCode},{bankAccount.AccountCode},{bankAccount.AccountNumber},{payment.SupplierCodeSnapshot},\"{payment.SupplierLegalNameSnapshot.Replace("\"", "'")}\",\"{payment.BeneficiaryBankName.Replace("\"", "'")}\",{payment.BeneficiaryAccountNumber},\"{payment.BeneficiaryAccountHolder.Replace("\"", "'")}\",{payment.PaymentMethod},{payment.PaymentDate:yyyy-MM-dd},{a.PayableNumberSnapshot},{a.SupplierInvoiceNumberSnapshot},{payment.CurrencyCode},{a.AllocatedAmount:F2}"));
            }

            var instruction = PaymentInstruction.RecordSimulation(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                instructionNumber,
                payment.SupplierPaymentId,
                bankAccount.BankAccountId,
                format,
                payment.TotalAllocatedAmount,
                payment.CurrencyCode,
                sb.ToString(),
                cmd.ActorUserId,
                cmd.NowUtc);

            db.PaymentInstructions.Add(instruction);
            payment.RecordInstructionSimulation(instruction.PaymentInstructionId, instruction.InstructionNumber, cmd.NowUtc);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_PAYMENT_INSTRUCTION_SIMULATED",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Generated non-live simulated payment instruction {instruction.InstructionNumber} (0 GL / 0 AP impact pending release)",
                JsonSerializer.Serialize(new { payment.SupplierPaymentId, instruction.PaymentInstructionId, instruction.InstructionNumber, instruction.PayloadSha256, SimulationOnly = true }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return SupplierPaymentOutcome.Success(payment, allocations, instruction);
        }
        catch (ArgumentException ex)
        {
            return SupplierPaymentOutcome.Fail(400, "payment_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
    }

    public static async Task<SupplierPaymentOutcome> SubmitPaymentForReleaseAsync(
        AtheraDbContext db,
        SubmitPaymentCommand cmd,
        CancellationToken ct)
    {
        try
        {
            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                return SupplierPaymentOutcome.Fail(404, "payment_not_found", "Supplier payment was not found.");
            }

            var allocations = await db.PaymentAllocations.AsNoTracking()
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SupplierPaymentId == payment.SupplierPaymentId)
                .OrderBy(a => a.LineNumber)
                .ToListAsync(ct);

            payment.SubmitForRelease(cmd.ActorUserId, cmd.NowUtc);

            var policy = await CashCoordinatorHelpers.EnsurePaymentReleasePolicyAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.NowUtc, ct);
            var existingPending = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == payment.SupplierPaymentId && a.Status == DocumentApprovalStatuses.Pending)
                .ToListAsync(ct);
            foreach (var old in existingPending)
            {
                old.Cancel(cmd.NowUtc);
            }

            var revNo = Math.Max(1, await db.DocumentApprovals.CountAsync(
                a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == payment.SupplierPaymentId, ct) + 1);

            db.DocumentApprovals.Add(DocumentApproval.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                policy.PolicyId,
                "CASH",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId,
                payment.SupplierPaymentId,
                revNo,
                $"Release supplier payment {payment.DraftCode} ({payment.TotalAllocatedAmount:N0} {payment.CurrencyCode})",
                cmd.ActorUserId,
                cmd.NowUtc));

            var payload = JsonSerializer.Serialize(new { payment.SupplierPaymentId, payment.DraftCode, payment.TotalAllocatedAmount });
            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "AP_PAYMENT_SUBMITTED",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Submitted supplier payment {payment.DraftCode} for independent four-eyes release",
                payload,
                cmd.NowUtc));
            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "cash.supplier_payment.submitted.v1",
                $"cash:pay-submit:{payment.SupplierPaymentId:D}:{revNo}",
                payload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            return SupplierPaymentOutcome.Success(payment, allocations);
        }
        catch (InvalidOperationException ex)
        {
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
    }

    public static async Task<SupplierPaymentOutcome> ReleasePaymentAsync(
        AtheraDbContext db,
        ReleasePaymentCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("ReleasePaymentAsync requires active entity transaction.");
        const string sp = "sp_release_supplier_payment";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            if (string.IsNullOrWhiteSpace(cmd.ReleaseReference))
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(400, "release_reference_required", "OD-06 requires an explicit portal/bank release reference to authorize outbound clearing.");
            }

            var idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);
            var cleanReleaseRef = GuardValidation.RequireName(cmd.ReleaseReference, nameof(cmd.ReleaseReference), 120);

            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payment_not_found", "Supplier payment was not found.");
            }

            var allocations = await db.PaymentAllocations
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SupplierPaymentId == payment.SupplierPaymentId)
                .OrderBy(a => a.LineNumber)
                .ToListAsync(ct);

            if (payment.Status is SupplierPaymentStatuses.Released or SupplierPaymentStatuses.PartiallyReconciled or SupplierPaymentStatuses.Reconciled)
            {
                if (!string.Equals(payment.ReleaseIdempotencyKey, idempKey, StringComparison.Ordinal))
                {
                    await tx.RollbackToSavepointAsync(sp, ct);
                    return SupplierPaymentOutcome.Fail(409, "payment_already_released", $"Payment '{payment.PaymentNumber ?? payment.DraftCode}' is already released.");
                }

                await tx.ReleaseSavepointAsync(sp, ct);
                var existingJrn = payment.ReleaseJournalId.HasValue
                    ? await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(
                        j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.JournalId == payment.ReleaseJournalId.Value, ct)
                    : null;
                return SupplierPaymentOutcome.Success(payment, allocations, journal: existingJrn, replay: true);
            }

            if (payment.Status != SupplierPaymentStatuses.PendingRelease)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(409, "payment_must_be_pending_release", $"Payment '{payment.DraftCode}' is '{payment.Status}' and must be submitted for four-eyes release first.");
            }

            if (cmd.ActorUserId == payment.PreparedByUserId || (payment.SubmittedByUserId.HasValue && cmd.ActorUserId == payment.SubmittedByUserId.Value))
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(403, "four_eyes_violation", "Four-eyes control (OD-02) forbids the payment preparer or submitter from releasing the payment.");
            }

            var payableIds = allocations.Select(a => a.PayableItemId).ToList();
            var lockedPayables = await CashCoordinatorHelpers.LockPayablesOrderedAsync(db, cmd.TenantId, cmd.LegalEntityId, payableIds, ct);
            if (lockedPayables.Count != payableIds.Count)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payable_not_found", "Linked payable item missing.");
            }

            var payableMap = lockedPayables.ToDictionary(p => p.PayableItemId);
            foreach (var alloc in allocations)
            {
                var payable = payableMap[alloc.PayableItemId];
                var openBefore = payable.RemainingOpenAmount;
                payable.ApplyPaymentSettlement(alloc.AllocatedAmount, cmd.NowUtc);
                alloc.RefreshSettlementSnapshots(openBefore, payable.RemainingOpenAmount);
            }

            var bankAccount = await db.BankAccounts.AsNoTracking().SingleAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == payment.BankAccountId, ct);
            var apControlMap = await db.AccountMappings.AsNoTracking().SingleOrDefaultAsync(
                m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId && m.PurposeCode == GlMappingPurposes.ApTradeControl, ct);
            if (apControlMap is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(409, "ap_control_mapping_missing", "Entity GL mapping AP_TRADE_CONTROL is required to release supplier payments.");
            }

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "ap.payment_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
            var paymentNumber = $"PAY-{cmd.PostingDate.Year:D4}-{seq:D5}";

            var journalLines = new List<GlJournalLineInput>
            {
                new(apControlMap.AccountId, $"Supplier payment release {paymentNumber} ({payment.SupplierCodeSnapshot}) Ref:{cleanReleaseRef}", DebitAmount: payment.TotalAllocatedAmount, CreditAmount: 0m, PartnerId: payment.SupplierPartnerId),
                new(bankAccount.ClearingGlAccountId, $"Outbound clearing {paymentNumber} ({bankAccount.AccountCode}) Ref:{cleanReleaseRef}", DebitAmount: 0m, CreditAmount: payment.TotalAllocatedAmount, PartnerId: payment.SupplierPartnerId)
            };

            var jrnOutcome = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    cmd.ExpectedFunctionalCurrencyCode,
                    cmd.PostingDate,
                    payment.CurrencyCode,
                    $"Release supplier payment {paymentNumber} -> Outbound Clearing 1120 ({payment.SupplierLegalNameSnapshot})",
                    "AP_CASH",
                    "SUPPLIER_PAYMENT_RELEASE",
                    payment.SupplierPaymentId.ToString("D"),
                    $"cash:pay-release:{payment.SupplierPaymentId:D}",
                    journalLines,
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!jrnOutcome.Succeeded || jrnOutcome.Journal is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(jrnOutcome.FailureHttpStatus, jrnOutcome.ErrorCode ?? "gl_posting_failed", jrnOutcome.ErrorDetail ?? "GL release posting failed.");
            }

            payment.ReleaseToClearing(paymentNumber, cleanReleaseRef, idempKey, jrnOutcome.Journal.JournalId, cmd.ActorUserId, cmd.NowUtc);

            var pendingApproval = await db.DocumentApprovals
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SourceDocumentId == payment.SupplierPaymentId && a.Status == DocumentApprovalStatuses.Pending)
                .OrderByDescending(a => a.RevisionNumber)
                .FirstOrDefaultAsync(ct);
            pendingApproval?.RecordDecision(cmd.ActorUserId, ApprovalDecisions.Approved, $"Released {paymentNumber} Ref: {cleanReleaseRef}", false, cmd.NowUtc);

            var outboxPayload = JsonSerializer.Serialize(new
            {
                payment.SupplierPaymentId,
                paymentNumber,
                payment.TotalAllocatedAmount,
                payment.CurrencyCode,
                ReleaseReference = cleanReleaseRef,
                jrnOutcome.Journal.JournalId,
                jrnOutcome.Journal.JournalNumber
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_PAYMENT_RELEASED_TO_CLEARING",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Released {paymentNumber} ({payment.TotalAllocatedAmount:N0} {payment.CurrencyCode}) Ref:{cleanReleaseRef} -> {jrnOutcome.Journal.JournalNumber} (Dr 2100 / Cr 1120)",
                outboxPayload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "cash.supplier_payment.released.v1",
                $"cash:pay-release:{payment.SupplierPaymentId:D}",
                outboxPayload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            if (cmd.OnAfterGlPostedBeforeCommitAsync is not null)
            {
                await cmd.OnAfterGlPostedBeforeCommitAsync();
            }

            await tx.ReleaseSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Success(payment, allocations, journal: jrnOutcome.Journal);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Fail(400, "payment_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
        catch
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            throw;
        }
    }

    public static async Task<SupplierPaymentOutcome> RejectReleasedPaymentAsync(
        AtheraDbContext db,
        RejectReleasedPaymentCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("RejectReleasedPaymentAsync requires active entity transaction.");
        const string sp = "sp_reject_released_payment";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(404, "payment_not_found", "Supplier payment was not found.");
            }

            if (payment.Status != SupplierPaymentStatuses.Released || payment.ReconciledAmount != 0m || !payment.ReleaseJournalId.HasValue)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(
                    409,
                    "payment_already_reconciled_reverse_match_first",
                    "Only unreconciled RELEASED payments can be bank-rejected directly; reverse any posted bank reconciliation matches first.");
            }

            var revOutcome = await GlPostingKernel.ReverseJournalAsync(
                db,
                new GlReverseJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    cmd.ExpectedFunctionalCurrencyCode,
                    payment.ReleaseJournalId.Value,
                    cmd.ReversalPostingDate,
                    $"Bank payment rejection: {cmd.Reason}",
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!revOutcome.Succeeded || revOutcome.Journal is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return SupplierPaymentOutcome.Fail(revOutcome.FailureHttpStatus, revOutcome.ErrorCode ?? "reversal_failed", revOutcome.ErrorDetail ?? "GL reversal failed.");
            }

            var allocations = await db.PaymentAllocations
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.SupplierPaymentId == payment.SupplierPaymentId)
                .OrderBy(a => a.LineNumber)
                .ToListAsync(ct);

            var lockedPayables = await CashCoordinatorHelpers.LockPayablesOrderedAsync(
                db, cmd.TenantId, cmd.LegalEntityId, allocations.Select(a => a.PayableItemId).ToList(), ct);
            var payableMap = lockedPayables.ToDictionary(p => p.PayableItemId);

            foreach (var alloc in allocations)
            {
                payableMap[alloc.PayableItemId].ReversePaymentSettlement(alloc.AllocatedAmount, cmd.NowUtc);
            }

            payment.MarkBankRejected(revOutcome.Journal.JournalId, cmd.Reason, cmd.ActorUserId, cmd.NowUtc);

            var payload = JsonSerializer.Serialize(new
            {
                payment.SupplierPaymentId,
                payment.PaymentNumber,
                ReversalJournalId = revOutcome.Journal.JournalId,
                revOutcome.Journal.JournalNumber,
                cmd.Reason
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_PAYMENT_BANK_REJECTED",
                "SUPPLIER_PAYMENT",
                payment.SupplierPaymentId.ToString("D"),
                $"Reversed rejected payment {payment.PaymentNumber} via {revOutcome.Journal.JournalNumber} (Dr 1120 / Cr 2100); payables restored",
                payload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "cash.supplier_payment.bank_rejected.v1",
                $"cash:pay-reject:{payment.SupplierPaymentId:D}",
                payload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Success(payment, allocations, journal: revOutcome.Journal);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return SupplierPaymentOutcome.Fail(400, "payment_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return SupplierPaymentOutcome.Fail(status, code, detail);
        }
    }

    private static async Task<SupplierPaymentOutcome?> ValidateProposalPayablesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid supplierPartnerId,
        string currencyCode,
        Guid? excludingPaymentId,
        IReadOnlyList<PaymentAllocationInput> inputs,
        IReadOnlyDictionary<Guid, PayableItem> payableMap,
        CancellationToken ct)
    {
        var ids = inputs.Select(i => i.PayableItemId).ToList();
        var pendingOtherAllocs = await db.PaymentAllocations.AsNoTracking()
            .Join(
                db.SupplierPayments.AsNoTracking(),
                a => new { a.TenantId, a.LegalEntityId, a.SupplierPaymentId },
                p => new { p.TenantId, p.LegalEntityId, p.SupplierPaymentId },
                (a, p) => new { a.TenantId, a.LegalEntityId, a.PayableItemId, a.AllocatedAmount, p.SupplierPaymentId, p.Status })
            .Where(x => x.TenantId == tenantId &&
                        x.LegalEntityId == entityId &&
                        ids.Contains(x.PayableItemId) &&
                        x.SupplierPaymentId != (excludingPaymentId ?? Guid.Empty) &&
                        (x.Status == SupplierPaymentStatuses.Draft || x.Status == SupplierPaymentStatuses.PendingRelease))
            .GroupBy(x => x.PayableItemId)
            .Select(g => new { PayableItemId = g.Key, Committed = g.Sum(x => x.AllocatedAmount) })
            .ToDictionaryAsync(x => x.PayableItemId, x => x.Committed, ct);

        foreach (var input in inputs)
        {
            var payable = payableMap[input.PayableItemId];
            if (payable.SupplierPartnerId != supplierPartnerId)
            {
                return SupplierPaymentOutcome.Fail(409, "payable_supplier_mismatch", $"Payable '{payable.PayableNumber}' belongs to a different supplier.");
            }

            if (payable.CurrencyCode != currencyCode)
            {
                return SupplierPaymentOutcome.Fail(409, "payable_currency_mismatch", $"Payable '{payable.PayableNumber}' currency does not match bank account currency '{currencyCode}'.");
            }

            if (payable.PaymentBlockStatus == PaymentBlockStatuses.PaymentBlocked)
            {
                return SupplierPaymentOutcome.Fail(409, "payable_payment_blocked", $"Payable '{payable.PayableNumber}' is on PAYMENT_BLOCKED hold ({payable.PaymentBlockReason}).");
            }

            if (payable.Status is not (PayableStatuses.Open or PayableStatuses.PartiallyPaid))
            {
                return SupplierPaymentOutcome.Fail(409, "payable_not_open", $"Payable '{payable.PayableNumber}' is in status '{payable.Status}'.");
            }

            var reservedElsewhere = pendingOtherAllocs.GetValueOrDefault(payable.PayableItemId, 0m);
            var availableToAllocate = decimal.Round(payable.RemainingOpenAmount - reservedElsewhere, 4, MidpointRounding.AwayFromZero);
            var requested = decimal.Round(input.AllocatedAmount, 4, MidpointRounding.AwayFromZero);

            if (requested <= 0m)
            {
                return SupplierPaymentOutcome.Fail(400, "invalid_allocation_amount", "Allocated amount must be positive.");
            }

            if (requested > availableToAllocate)
            {
                return SupplierPaymentOutcome.Fail(
                    409,
                    "allocation_exceeds_open_payable",
                    $"Allocation ({requested:N0}) exceeds available open balance ({availableToAllocate:N0}) on '{payable.PayableNumber}' (after reserving {reservedElsewhere:N0} in pending proposals).");
            }
        }

        return null;
    }
}
