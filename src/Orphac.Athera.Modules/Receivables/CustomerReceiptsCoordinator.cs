using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Receivables;

public sealed record ReceiptAllocationInput(
    Guid ReceivableId,
    decimal AllocatedAmount);

public sealed record ConfirmCustomerReceiptCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid BankStatementLineId,
    Guid CustomerPartnerId,
    DateOnly ReceiptDate,
    string IdempotencyKey,
    string? Notes,
    IReadOnlyList<ReceiptAllocationInput>? InitialAllocations,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record AllocateCustomerReceiptCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ReceiptId,
    IReadOnlyList<ReceiptAllocationInput> Allocations,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReverseCustomerReceiptCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid ReceiptId,
    DateOnly ReversalDate,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record ReceivablesOutcome<T>(
    bool Succeeded,
    int StatusCode,
    string? ErrorCode,
    string? Title,
    string? Detail,
    T? Value)
{
    public static ReceivablesOutcome<T> Ok(T value) => new(true, 200, null, null, null, value);
    public static ReceivablesOutcome<T> Created(T value) => new(true, 201, null, null, null, value);
    public static ReceivablesOutcome<T> Fail(int statusCode, string errorCode, string title, string detail) =>
        new(false, statusCode, errorCode, title, detail, default);
}

public sealed record ReceiptWithAllocations(
    CustomerReceipt Receipt,
    IReadOnlyList<ReceiptAllocation> Allocations);

public static class CustomerReceiptsCoordinator
{
    public static async Task<ReceivablesOutcome<ReceiptWithAllocations>> ConfirmReceiptAsync(
        AtheraDbContext db,
        ConfirmCustomerReceiptCommand cmd,
        CancellationToken ct)
    {
        string idempKey;
        try
        {
            idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);
        }
        catch (ArgumentException ex)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(400, "ar_idempotency_key_invalid", "Invalid idempotency key", ex.Message);
        }

        var existing = await db.CustomerReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == cmd.TenantId && r.LegalEntityId == cmd.LegalEntityId && r.IdempotencyKey == idempKey, ct);
        if (existing is not null)
        {
            var existingAllocs = await db.ReceiptAllocations.AsNoTracking()
                .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.ReceiptId == existing.ReceiptId)
                .OrderBy(a => a.AllocatedAtUtc)
                .ToListAsync(ct);
            return ReceivablesOutcome<ReceiptWithAllocations>.Ok(new ReceiptWithAllocations(existing, existingAllocs));
        }

        // Lock inbound CREDIT bank statement line FOR UPDATE (prevents double collection)
        var stmtLine = await db.BankStatementLines
            .FromSqlInterpolated($"""
                SELECT * FROM cash.bank_statement_lines
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND bank_statement_line_id = {cmd.BankStatementLineId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (stmtLine is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(404, "ar_bank_line_not_found", "Bank statement line not found", "The selected inbound bank statement line was not found in this entity.");
        }

        if (stmtLine.Direction != BankStatementLineDirections.Credit)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "statement_line_direction_invalid", "Invalid bank line direction", "Customer receipts require an inbound CREDIT bank statement line.");
        }

        if (stmtLine.Status != BankStatementLineStatuses.Unmatched || stmtLine.UnmatchedAmount <= 0m)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "ar_bank_line_already_recognized", "Bank line already recognized", $"Bank statement line '{stmtLine.BankReference}' is already '{stmtLine.Status}' and cannot be recognized twice.");
        }

        var customer = await db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == cmd.TenantId && p.PartnerId == cmd.CustomerPartnerId && p.IsCustomer, ct);
        if (customer is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(404, "ar_customer_not_found", "Customer not found", "Selected customer partner does not exist or is not marked as a customer.");
        }

        var bankAccount = await db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == stmtLine.BankAccountId, ct);
        if (bankAccount is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(404, "ar_bank_account_not_found", "Bank account not found", "Associated entity bank account could not be resolved.");
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId)
            .ToDictionaryAsync(m => m.PurposeCode, m => m.AccountId, StringComparer.Ordinal, ct);

        if (!mappings.TryGetValue(GlMappingPurposes.ArTradeControl, out var arAccountId))
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "ar_gl_mapping_missing", "Missing AR Control GL mapping", "Entity must configure AR_TRADE_CONTROL (1200) mapping.");
        }

        var bankGlAccountId = bankAccount.GlAccountId != Guid.Empty
            ? bankAccount.GlAccountId
            : (mappings.TryGetValue(GlMappingPurposes.BankMainOperating, out var fallbackBank) ? fallbackBank : Guid.Empty);
        if (bankGlAccountId == Guid.Empty)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "ar_bank_gl_missing", "Missing Bank GL mapping", "Bank account must be linked to an operating Cash/Bank GL account.");
        }

        var receiptAmount = decimal.Round(stmtLine.UnmatchedAmount, 2, MidpointRounding.AwayFromZero);
        var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
            db, "ar.receipt_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.ReceiptDate.Year, ct);
        var receiptNumber = $"RCT-{cmd.ReceiptDate.Year}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
        var receiptId = Guid.NewGuid();

        var journalOutcome = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                "IDR",
                cmd.ReceiptDate,
                "IDR",
                $"Penerimaan Pelanggan {receiptNumber} · {customer.PartnerCode} (Ref Bank {stmtLine.BankReference})",
                "RECEIVABLES",
                "CUSTOMER_RECEIPT",
                receiptNumber,
                $"ar-rct-confirm:{receiptId:N}",
                [
                    new GlJournalLineInput(bankGlAccountId, $"Kas/Bank Masuk {receiptNumber} ({stmtLine.BankReference})", receiptAmount, 0m, customer.PartnerId, null, null),
                    new GlJournalLineInput(arAccountId, $"Pelunasan/Uang Muka AR {receiptNumber} · {customer.PartnerCode}", 0m, receiptAmount, customer.PartnerId, null, null)
                ],
                cmd.ActorUserId,
                cmd.NowUtc),
            ct);

        if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(
                journalOutcome.FailureHttpStatus,
                journalOutcome.ErrorCode ?? "gl_posting_failed",
                "GL posting rejected",
                journalOutcome.ErrorDetail ?? "Failed to post customer receipt journal entry.");
        }

        stmtLine.ApplyInboundReceiptMatch(receiptAmount, cmd.NowUtc);

        var receipt = CustomerReceipt.CreateConfirmed(
            receiptId,
            cmd.TenantId,
            cmd.LegalEntityId,
            receiptNumber,
            stmtLine.BankAccountId,
            stmtLine.BankStatementId,
            stmtLine.BankStatementLineId,
            customer.PartnerId,
            customer.PartnerCode,
            customer.LegalName,
            cmd.ReceiptDate,
            stmtLine.BankReference,
            "IDR",
            receiptAmount,
            journalOutcome.Journal.JournalId,
            journalOutcome.Journal.JournalNumber,
            idempKey,
            cmd.Notes,
            cmd.ActorUserId,
            cmd.NowUtc);

        db.CustomerReceipts.Add(receipt);

        var createdAllocs = new List<ReceiptAllocation>();
        if (cmd.InitialAllocations is { Count: > 0 })
        {
            var allocOutcome = await ApplyAllocationsInternalAsync(db, receipt, cmd.InitialAllocations, cmd.ActorUserId, cmd.NowUtc, ct);
            if (!allocOutcome.Succeeded)
            {
                return ReceivablesOutcome<ReceiptWithAllocations>.Fail(
                    allocOutcome.StatusCode, allocOutcome.ErrorCode!, allocOutcome.Title!, allocOutcome.Detail!);
            }

            createdAllocs.AddRange(allocOutcome.Value!);
        }

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "ar.customer_receipt.confirmed",
            receipt.ReceiptId.ToString("D"),
            JsonSerializer.Serialize(new
            {
                receipt.ReceiptId,
                receipt.ReceiptNumber,
                receipt.CustomerCodeSnapshot,
                receipt.ReceiptAmount,
                receipt.AllocatedAmount,
                receipt.UnappliedAmount,
                receipt.GlJournalNumber
            }),
            cmd.NowUtc));

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "AR_RECEIPT_CONFIRMED",
            "CustomerReceipt",
            receipt.ReceiptId.ToString("D"),
            $"Confirmed receipt {receipt.ReceiptNumber} ({receipt.ReceiptAmount:0.00} IDR) from bank ref {stmtLine.BankReference}",
            JsonSerializer.Serialize(new { receipt.ReceiptNumber, receipt.ReceiptAmount, receipt.AllocatedAmount, receipt.UnappliedAmount, receipt.GlJournalNumber }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return ReceivablesOutcome<ReceiptWithAllocations>.Created(new ReceiptWithAllocations(receipt, createdAllocs));
    }

    public static async Task<ReceivablesOutcome<ReceiptWithAllocations>> AllocateReceiptAsync(
        AtheraDbContext db,
        AllocateCustomerReceiptCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Allocations is null || cmd.Allocations.Count == 0)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(400, "ar_allocations_required", "Allocations required", "At least one invoice allocation line must be provided.");
        }

        var receipt = await db.CustomerReceipts
            .FromSqlInterpolated($"""
                SELECT * FROM ar.customer_receipts
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND receipt_id = {cmd.ReceiptId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (receipt is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(404, "ar_receipt_not_found", "Receipt not found", "Customer receipt not found in this entity.");
        }

        if (receipt.Status == CustomerReceiptStatuses.Reversed)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "ar_receipt_already_reversed", "Receipt reversed", "Reversed customer receipts cannot be allocated.");
        }

        var allocOutcome = await ApplyAllocationsInternalAsync(db, receipt, cmd.Allocations, cmd.ActorUserId, cmd.NowUtc, ct);
        if (!allocOutcome.Succeeded)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(
                allocOutcome.StatusCode, allocOutcome.ErrorCode!, allocOutcome.Title!, allocOutcome.Detail!);
        }

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "AR_RECEIPT_ALLOCATED",
            "CustomerReceipt",
            receipt.ReceiptId.ToString("D"),
            $"Allocated receipt {receipt.ReceiptNumber} (allocated: {receipt.AllocatedAmount:0.00}, unapplied: {receipt.UnappliedAmount:0.00})",
            JsonSerializer.Serialize(new { receipt.ReceiptNumber, receipt.AllocatedAmount, receipt.UnappliedAmount, count = allocOutcome.Value!.Count }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);

        var allAllocs = await db.ReceiptAllocations.AsNoTracking()
            .Where(a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.ReceiptId == receipt.ReceiptId)
            .OrderBy(a => a.AllocatedAtUtc)
            .ToListAsync(ct);

        return ReceivablesOutcome<ReceiptWithAllocations>.Ok(new ReceiptWithAllocations(receipt, allAllocs));
    }

    public static async Task<ReceivablesOutcome<ReceiptWithAllocations>> ReverseReceiptAsync(
        AtheraDbContext db,
        ReverseCustomerReceiptCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(400, "ar_reversal_reason_required", "Reversal reason required", "An explicit operational reason is required to reverse a customer receipt.");
        }

        var receipt = await db.CustomerReceipts
            .FromSqlInterpolated($"""
                SELECT * FROM ar.customer_receipts
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND receipt_id = {cmd.ReceiptId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(ct);

        if (receipt is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(404, "ar_receipt_not_found", "Receipt not found", "Customer receipt not found in this entity.");
        }

        if (receipt.Status == CustomerReceiptStatuses.Reversed)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(409, "ar_receipt_already_reversed", "Receipt already reversed", "This customer receipt has already been reversed.");
        }

        var stmtLine = await db.BankStatementLines
            .FromSqlInterpolated($"""
                SELECT * FROM cash.bank_statement_lines
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND bank_statement_line_id = {receipt.BankStatementLineId}
                FOR UPDATE
                """)
            .SingleAsync(ct);

        var bankAccount = await db.BankAccounts.AsNoTracking()
            .SingleAsync(b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == receipt.BankAccountId, ct);

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId)
            .ToDictionaryAsync(m => m.PurposeCode, m => m.AccountId, StringComparer.Ordinal, ct);

        var arAccountId = mappings[GlMappingPurposes.ArTradeControl];
        var bankGlAccountId = bankAccount.GlAccountId != Guid.Empty
            ? bankAccount.GlAccountId
            : mappings[GlMappingPurposes.BankMainOperating];

        var journalOutcome = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                receipt.CurrencyCode,
                cmd.ReversalDate,
                receipt.CurrencyCode,
                $"Pembalikan Penerimaan Pelanggan {receipt.ReceiptNumber} · {cmd.Reason.Trim()}",
                "RECEIVABLES",
                "CUSTOMER_RECEIPT_REVERSAL",
                receipt.ReceiptNumber,
                $"ar-rct-reverse:{receipt.ReceiptId:N}",
                [
                    new GlJournalLineInput(arAccountId, $"Pemulihan Piutang {receipt.ReceiptNumber}", receipt.ReceiptAmount, 0m, receipt.CustomerPartnerId, null, null),
                    new GlJournalLineInput(bankGlAccountId, $"Pembalikan Kas/Bank {receipt.ReceiptNumber}", 0m, receipt.ReceiptAmount, receipt.CustomerPartnerId, null, null)
                ],
                cmd.ActorUserId,
                cmd.NowUtc,
                JournalEntryKinds.Reversal,
                receipt.GlJournalId),
            ct);

        if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
        {
            return ReceivablesOutcome<ReceiptWithAllocations>.Fail(
                journalOutcome.FailureHttpStatus,
                journalOutcome.ErrorCode ?? "gl_posting_failed",
                "GL reversal rejected",
                journalOutcome.ErrorDetail ?? "Failed to post compensating receipt reversal journal.");
        }

        // Restore all active allocations -> restore open AR balances
        var activeAllocs = await db.ReceiptAllocations
            .Where(a => a.TenantId == cmd.TenantId
                && a.LegalEntityId == cmd.LegalEntityId
                && a.ReceiptId == receipt.ReceiptId
                && a.Status == ReceiptAllocationStatuses.Active)
            .OrderBy(a => a.ReceivableId)
            .ToListAsync(ct);

        var recIds = activeAllocs.Select(a => a.ReceivableId).Distinct().OrderBy(id => id).ToList();
        var recMap = new Dictionary<Guid, CustomerReceivable>();
        foreach (var rid in recIds)
        {
            var rec = await db.CustomerReceivables
                .FromSqlInterpolated($"""
                    SELECT * FROM ar.customer_receivables
                    WHERE tenant_id = {cmd.TenantId}
                      AND legal_entity_id = {cmd.LegalEntityId}
                      AND receivable_id = {rid}
                    FOR UPDATE
                    """)
                .SingleAsync(ct);
            recMap[rid] = rec;
        }

        foreach (var alloc in activeAllocs)
        {
            recMap[alloc.ReceivableId].ReverseReceipt(alloc.AllocatedAmount, cmd.NowUtc);
            alloc.MarkReversed(cmd.ActorUserId, cmd.NowUtc);
        }

        stmtLine.ReverseMatch(receipt.ReceiptAmount, cmd.NowUtc);
        receipt.MarkReversed(journalOutcome.Journal.JournalId, journalOutcome.Journal.JournalNumber, cmd.Reason, cmd.ActorUserId, cmd.NowUtc);

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            "ar.customer_receipt.reversed",
            receipt.ReceiptId.ToString("D"),
            JsonSerializer.Serialize(new
            {
                receipt.ReceiptId,
                receipt.ReceiptNumber,
                receipt.ReceiptAmount,
                receipt.ReversalGlJournalNumber,
                receipt.ReversalReason
            }),
            cmd.NowUtc));

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            "AR_RECEIPT_REVERSED",
            "CustomerReceipt",
            receipt.ReceiptId.ToString("D"),
            $"Reversed receipt {receipt.ReceiptNumber} ({journalOutcome.Journal.JournalNumber}): {cmd.Reason.Trim()}",
            JsonSerializer.Serialize(new { receipt.ReceiptNumber, receipt.ReversalGlJournalNumber, receipt.ReversalReason }),
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return ReceivablesOutcome<ReceiptWithAllocations>.Ok(new ReceiptWithAllocations(receipt, activeAllocs));
    }

    private static async Task<ReceivablesOutcome<List<ReceiptAllocation>>> ApplyAllocationsInternalAsync(
        AtheraDbContext db,
        CustomerReceipt receipt,
        IReadOnlyList<ReceiptAllocationInput> inputs,
        Guid actorUserId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var grouped = inputs
            .GroupBy(x => x.ReceivableId)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.AllocatedAmount), 2, MidpointRounding.AwayFromZero));

        if (grouped.Values.Any(v => v <= 0m))
        {
            return ReceivablesOutcome<List<ReceiptAllocation>>.Fail(400, "ar_allocation_must_be_positive", "Invalid allocation amount", "Every allocation amount must be strictly positive.");
        }

        var totalReq = decimal.Round(grouped.Values.Sum(), 2, MidpointRounding.AwayFromZero);
        if (totalReq > receipt.UnappliedAmount)
        {
            return ReceivablesOutcome<List<ReceiptAllocation>>.Fail(
                409, "ar_allocation_exceeds_unapplied", "Allocation exceeds unapplied receipt balance",
                $"Total requested allocation ({totalReq:0.00}) exceeds unapplied receipt balance ({receipt.UnappliedAmount:0.00}).");
        }

        var orderedIds = grouped.Keys.OrderBy(id => id).ToList();
        var created = new List<ReceiptAllocation>(orderedIds.Count);

        foreach (var rid in orderedIds)
        {
            var rec = await db.CustomerReceivables
                .FromSqlInterpolated($"""
                    SELECT * FROM ar.customer_receivables
                    WHERE tenant_id = {receipt.TenantId}
                      AND legal_entity_id = {receipt.LegalEntityId}
                      AND receivable_id = {rid}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(ct);

            if (rec is null)
            {
                return ReceivablesOutcome<List<ReceiptAllocation>>.Fail(
                    404, "ar_receivable_not_found", "Receivable not found",
                    "Target customer receivable was not found in this legal entity.");
            }

            if (rec.CustomerPartnerId != receipt.CustomerPartnerId)
            {
                return ReceivablesOutcome<List<ReceiptAllocation>>.Fail(
                    409, "ar_customer_mismatch", "Customer mismatch",
                    $"Receivable {rec.ReceivableNumber} belongs to a different customer than receipt {receipt.ReceiptNumber}.");
            }

            var allocAmt = grouped[rid];
            if (allocAmt > rec.OpenAmount)
            {
                return ReceivablesOutcome<List<ReceiptAllocation>>.Fail(
                    409, "ar_allocation_exceeds_open_amount", "Allocation exceeds open invoice balance",
                    $"Requested allocation ({allocAmt:0.00}) exceeds open AR balance ({rec.OpenAmount:0.00}) on {rec.InvoiceNumberSnapshot}.");
            }

            rec.ApplyReceipt(allocAmt, nowUtc);
            receipt.ApplyAllocation(allocAmt, nowUtc);

            var alloc = ReceiptAllocation.Create(
                Guid.NewGuid(),
                receipt.TenantId,
                receipt.LegalEntityId,
                receipt.ReceiptId,
                rec.ReceivableId,
                rec.InvoiceId,
                rec.InvoiceNumberSnapshot,
                allocAmt,
                actorUserId,
                nowUtc);

            db.ReceiptAllocations.Add(alloc);
            created.Add(alloc);
        }

        return ReceivablesOutcome<List<ReceiptAllocation>>.Ok(created);
    }
}
