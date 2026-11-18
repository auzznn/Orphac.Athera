using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.GeneralLedger;

public sealed record GlPostJournalCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    DateOnly PostingDate,
    string CurrencyCode,
    string Description,
    string SourceModule,
    string SourceDocumentType,
    string SourceDocumentId,
    string SourceEventKey,
    IReadOnlyList<GlJournalLineInput> Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc,
    string EntryKind = JournalEntryKinds.Standard,
    Guid? ReversalOfJournalId = null);

public sealed record GlReverseJournalCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid TargetJournalId,
    DateOnly ReversalPostingDate,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record GlPostingOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    JournalEntry? Journal,
    IReadOnlyList<JournalLine> Lines,
    string? PeriodCode)
{
    public static GlPostingOutcome Fail(int statusCode, string errorCode, string detail) =>
        new(false, false, statusCode, errorCode, detail, null, Array.Empty<JournalLine>(), null);

    public static GlPostingOutcome Success(JournalEntry entry, IReadOnlyList<JournalLine> lines, string? periodCode, bool replay) =>
        new(true, replay, replay ? 200 : 201, null, null, entry, lines, periodCode);
}

/// <summary>
/// Authoritative transactional posting kernel shared by S04 and downstream subledgers (S07-S15).
/// Caller MUST run inside a scoped PostgreSQL transaction (`ExecuteInEntityAsync`).
/// </summary>
public static class GlPostingKernel
{
    public static async Task<GlPostingOutcome> PostJournalAsync(
        AtheraDbContext db,
        GlPostJournalCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Lines is null || cmd.Lines.Count < 2)
        {
            return GlPostingOutcome.Fail(400, "invalid_journal_lines", "At least 2 journal lines are required.");
        }

        string normalizedCurrency;
        string normalizedSourceModule;
        string normalizedSourceDocType;
        string normalizedSourceDocId;
        string normalizedSourceKey;
        string normalizedDescription;
        string payloadHash;

        try
        {
            var expectedCurrency = GuardValidation.RequireExactIsoCode(
                cmd.ExpectedFunctionalCurrencyCode, "IDR", 3, nameof(cmd.ExpectedFunctionalCurrencyCode));
            normalizedCurrency = GuardValidation.RequireExactIsoCode(
                cmd.CurrencyCode, expectedCurrency, 3, nameof(cmd.CurrencyCode));

            if (!string.Equals(normalizedCurrency, expectedCurrency, StringComparison.Ordinal))
            {
                return GlPostingOutcome.Fail(
                    400,
                    "unsupported_currency_mvp",
                    $"S04 ledger MVP requires legal-entity functional currency '{expectedCurrency}'.");
            }

            decimal debitSum = 0m;
            decimal creditSum = 0m;
            foreach (var line in cmd.Lines)
            {
                if (line.AccountId == Guid.Empty)
                {
                    return GlPostingOutcome.Fail(400, "invalid_journal_lines", "Every journal line requires an AccountId.");
                }

                if (line.DebitAmount < 0m || line.CreditAmount < 0m ||
                    decimal.Round(line.DebitAmount, 4, MidpointRounding.AwayFromZero) != line.DebitAmount ||
                    decimal.Round(line.CreditAmount, 4, MidpointRounding.AwayFromZero) != line.CreditAmount)
                {
                    return GlPostingOutcome.Fail(400, "invalid_journal_lines", "Amounts must be non-negative with at most 4 decimals.");
                }

                var validSide = (line.DebitAmount > 0m && line.CreditAmount == 0m) ||
                                (line.CreditAmount > 0m && line.DebitAmount == 0m);
                if (!validSide)
                {
                    return GlPostingOutcome.Fail(400, "invalid_journal_lines", "Each line must carry either a positive Debit or positive Credit (never both or zero).");
                }

                debitSum += line.DebitAmount;
                creditSum += line.CreditAmount;
            }

            if (debitSum <= 0m || debitSum != creditSum)
            {
                return GlPostingOutcome.Fail(
                    400,
                    "unbalanced_journal",
                    $"Journal does not balance: Debit={debitSum:F4}, Credit={creditSum:F4}.");
            }

            normalizedSourceModule = GuardValidation.RequireMasterCode(cmd.SourceModule, nameof(cmd.SourceModule), 2, 40);
            normalizedSourceDocType = GuardValidation.RequireMasterCode(cmd.SourceDocumentType, nameof(cmd.SourceDocumentType), 2, 48);
            normalizedSourceDocId = GuardValidation.RequireName(cmd.SourceDocumentId, nameof(cmd.SourceDocumentId), 100);
            normalizedSourceKey = GuardValidation.RequireName(cmd.SourceEventKey, nameof(cmd.SourceEventKey), 160);
            normalizedDescription = GuardValidation.RequireName(cmd.Description, nameof(cmd.Description), 300);

            payloadHash = GlPostingHash.ComputePayloadHash(
                cmd.PostingDate,
                normalizedCurrency,
                cmd.EntryKind,
                cmd.ReversalOfJournalId,
                cmd.Lines);
        }
        catch (ArgumentException ex)
        {
            return GlPostingOutcome.Fail(400, "validation_error", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return GlPostingOutcome.Fail(400, "validation_error", ex.Message);
        }

        // Acquire PostgreSQL FOR UPDATE lock on the target financial period so period closes and
        // concurrent duplicate source submissions serialize cleanly within this legal entity/period.
        var matchingPeriods = await db.FinancialPeriods
            .FromSqlInterpolated(
                $"""
                SELECT period_id, tenant_id, legal_entity_id, period_code, fiscal_year, period_number,
                       start_date, end_date, status, closed_at_utc, closed_by_user_id, close_note,
                       created_at_utc, updated_at_utc, version
                FROM gl.financial_periods
                WHERE tenant_id = {cmd.TenantId}
                  AND legal_entity_id = {cmd.LegalEntityId}
                  AND start_date <= {cmd.PostingDate}
                  AND end_date >= {cmd.PostingDate}
                ORDER BY period_number ASC
                LIMIT 1
                FOR UPDATE
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        var period = matchingPeriods.FirstOrDefault();
        if (period is null)
        {
            return GlPostingOutcome.Fail(
                409,
                "period_not_found_for_date",
                $"No fiscal period is configured covering posting date {cmd.PostingDate:yyyy-MM-dd}.");
        }

        if (!period.IsOpenForPosting(cmd.PostingDate))
        {
            return GlPostingOutcome.Fail(
                409,
                "period_closed",
                $"Fiscal period '{period.PeriodCode}' is '{period.Status}' and refuses postings.");
        }

        var existing = await db.JournalEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                j => j.TenantId == cmd.TenantId
                    && j.LegalEntityId == cmd.LegalEntityId
                    && j.SourceModule == normalizedSourceModule
                    && j.SourceEventKey == normalizedSourceKey,
                ct);

        if (existing is not null)
        {
            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                return GlPostingOutcome.Fail(
                    409,
                    "duplicate_posting_source_conflict",
                    $"Source event '{normalizedSourceModule}:{normalizedSourceKey}' was already posted with a different payload.");
            }

            var existingLines = await db.JournalLines
                .AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.JournalId == existing.JournalId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            var existingPeriodCode = await db.FinancialPeriods
                .AsNoTracking()
                .Where(p => p.PeriodId == existing.PeriodId)
                .Select(p => p.PeriodCode)
                .SingleOrDefaultAsync(ct);

            return GlPostingOutcome.Success(existing, existingLines, existingPeriodCode, replay: true);
        }

        // Validate referenced accounts, partners, items and tax codes strictly inside tenant/entity scope.
        var requestedAccountIds = cmd.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await db.GlAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == cmd.TenantId
                && a.LegalEntityId == cmd.LegalEntityId
                && requestedAccountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, ct);

        foreach (var accountId in requestedAccountIds)
        {
            if (!accounts.TryGetValue(accountId, out var acc))
            {
                return GlPostingOutcome.Fail(
                    404,
                    "foreign_or_missing_account",
                    $"GL account '{accountId}' does not exist in this legal entity.");
            }

            if (acc.Status != MasterStatuses.Active)
            {
                return GlPostingOutcome.Fail(400, "account_inactive", $"GL account '{acc.AccountCode}' is inactive.");
            }

            if (!acc.AllowPosting)
            {
                return GlPostingOutcome.Fail(
                    400,
                    "account_not_postable",
                    $"GL account '{acc.AccountCode}' is a non-postable header or restricted account.");
            }
        }

        var partnerIds = cmd.Lines.Where(l => l.PartnerId.HasValue).Select(l => l.PartnerId!.Value).Distinct().ToList();
        if (partnerIds.Count > 0)
        {
            var foundCount = await db.Partners
                .AsNoTracking()
                .CountAsync(p => p.TenantId == cmd.TenantId && partnerIds.Contains(p.PartnerId), ct);
            if (foundCount != partnerIds.Count)
            {
                return GlPostingOutcome.Fail(404, "foreign_or_missing_partner", "Referenced partner not found in tenant.");
            }
        }

        var itemIds = cmd.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            var foundCount = await db.Items
                .AsNoTracking()
                .CountAsync(i => i.TenantId == cmd.TenantId && itemIds.Contains(i.ItemId), ct);
            if (foundCount != itemIds.Count)
            {
                return GlPostingOutcome.Fail(404, "foreign_or_missing_item", "Referenced item not found in tenant.");
            }
        }

        var taxCodeIds = cmd.Lines.Where(l => l.TaxCodeId.HasValue).Select(l => l.TaxCodeId!.Value).Distinct().ToList();
        if (taxCodeIds.Count > 0)
        {
            var foundCount = await db.TaxCodes
                .AsNoTracking()
                .CountAsync(t => t.TenantId == cmd.TenantId && t.LegalEntityId == cmd.LegalEntityId && taxCodeIds.Contains(t.TaxCodeId), ct);
            if (foundCount != taxCodeIds.Count)
            {
                return GlPostingOutcome.Fail(404, "foreign_or_missing_tax_code", "Referenced tax code not found in legal entity.");
            }
        }

        var seq = await IncrementPeriodSequenceAsync(db, cmd.TenantId, cmd.LegalEntityId, period.PeriodId, ct);
        var journalNumber = $"JRN-{period.PeriodCode}-{seq.ToString("D5", CultureInfo.InvariantCulture)}";
        var journalId = Guid.NewGuid();

        var (entry, lines) = JournalEntry.CreatePosted(
            journalId,
            cmd.TenantId,
            cmd.LegalEntityId,
            period.PeriodId,
            journalNumber,
            cmd.EntryKind,
            cmd.PostingDate,
            normalizedCurrency,
            normalizedDescription,
            normalizedSourceModule,
            normalizedSourceDocType,
            normalizedSourceDocId,
            normalizedSourceKey,
            cmd.ReversalOfJournalId,
            cmd.Lines,
            cmd.ActorUserId,
            cmd.NowUtc);

        db.JournalEntries.Add(entry);
        db.JournalLines.AddRange(lines);

        var isReversal = entry.EntryKind == JournalEntryKinds.Reversal;
        var payloadJson = JsonSerializer.Serialize(new
        {
            entry.JournalId,
            entry.JournalNumber,
            entry.EntryKind,
            entry.ReversalOfJournalId,
            PostingDate = entry.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            entry.CurrencyCode,
            entry.TotalDebit,
            entry.TotalCredit,
            entry.LineCount,
            entry.SourceModule,
            entry.SourceEventKey
        });

        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ActorUserId,
            isReversal ? "GL_JOURNAL_REVERSED" : "GL_JOURNAL_POSTED",
            "JOURNAL_ENTRY",
            entry.JournalId.ToString("D"),
            isReversal
                ? $"Posted reversal {entry.JournalNumber} ({entry.TotalDebit:F2} {entry.CurrencyCode})"
                : $"Posted journal {entry.JournalNumber} ({entry.TotalDebit:F2} {entry.CurrencyCode})",
            payloadJson,
            cmd.NowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            cmd.TenantId,
            cmd.LegalEntityId,
            isReversal ? "finance.journal.reversed.v1" : "finance.journal.posted.v1",
            $"gl:journal:{entry.JournalId:D}",
            payloadJson,
            cmd.NowUtc));

        await db.SaveChangesAsync(ct);
        return GlPostingOutcome.Success(entry, lines, period.PeriodCode, replay: false);
    }

    public static async Task<GlPostingOutcome> ReverseJournalAsync(
        AtheraDbContext db,
        GlReverseJournalCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            return GlPostingOutcome.Fail(400, "reversal_reason_required", "A written reason is required to post a compensating reversal journal.");
        }

        var original = await db.JournalEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.JournalId == cmd.TargetJournalId,
                ct);

        if (original is null)
        {
            return GlPostingOutcome.Fail(404, "journal_not_found", "Journal entry was not found in this legal entity.");
        }

        if (original.EntryKind == JournalEntryKinds.Reversal)
        {
            return GlPostingOutcome.Fail(409, "cannot_reverse_reversal_journal", "A compensating reversal journal cannot itself be reversed.");
        }

        var alreadyReversed = await db.JournalEntries
            .AsNoTracking()
            .AnyAsync(
                j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.ReversalOfJournalId == original.JournalId,
                ct);

        if (alreadyReversed)
        {
            return GlPostingOutcome.Fail(409, "journal_already_reversed", $"Journal '{original.JournalNumber}' has already been reversed.");
        }

        var originalLines = await db.JournalLines
            .AsNoTracking()
            .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.JournalId == original.JournalId)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        var invertedLines = originalLines
            .Select(l => new GlJournalLineInput(
                l.AccountId,
                string.IsNullOrWhiteSpace(l.Description) ? $"Reversal of {original.JournalNumber}" : $"Reversal: {l.Description}",
                DebitAmount: l.CreditAmount,
                CreditAmount: l.DebitAmount,
                PartnerId: l.PartnerId,
                ItemId: l.ItemId,
                TaxCodeId: l.TaxCodeId))
            .ToList();

        var reason = cmd.Reason.Trim();
        var description = $"Reversal of {original.JournalNumber}: {reason}";
        if (description.Length > 300)
        {
            description = description[..300];
        }

        var reverseCmd = new GlPostJournalCommand(
            cmd.TenantId,
            cmd.LegalEntityId,
            cmd.ExpectedFunctionalCurrencyCode,
            cmd.ReversalPostingDate,
            original.CurrencyCode,
            description,
            "GL_REVERSAL",
            "JOURNAL_REVERSAL",
            original.JournalId.ToString("D"),
            $"reversal:{original.JournalId:D}",
            invertedLines,
            cmd.ActorUserId,
            cmd.NowUtc,
            EntryKind: JournalEntryKinds.Reversal,
            ReversalOfJournalId: original.JournalId);

        return await PostJournalAsync(db, reverseCmd, ct);
    }

    private static async Task<long> IncrementPeriodSequenceAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid periodId,
        CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = """
            INSERT INTO gl.journal_sequences (tenant_id, legal_entity_id, period_id, last_number)
            VALUES (@tid, @eid, @pid, 1)
            ON CONFLICT (tenant_id, legal_entity_id, period_id)
            DO UPDATE SET last_number = gl.journal_sequences.last_number + 1
            RETURNING last_number;
            """;

        var pTid = cmd.CreateParameter();
        pTid.ParameterName = "tid";
        pTid.Value = tenantId;
        cmd.Parameters.Add(pTid);

        var pEid = cmd.CreateParameter();
        pEid.ParameterName = "eid";
        pEid.Value = entityId;
        cmd.Parameters.Add(pEid);

        var pPid = cmd.CreateParameter();
        pPid.ParameterName = "pid";
        pPid.Value = periodId;
        cmd.Parameters.Add(pPid);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public sealed record ReceiptAccrualLineInput(
        Guid ValuedAcceptanceLineId,
        Guid GoodsReceiptLineId,
        Guid PoLineId,
        int LineNumber,
        Guid ItemId,
        string ItemCode,
        decimal AcceptedBaseQuantity,
        decimal UnitCostBase,
        decimal AcceptedLineAmount);

    public sealed record PostReceiptAccrualCommand(
        Guid TenantId,
        Guid LegalEntityId,
        string ExpectedFunctionalCurrencyCode,
        DateOnly PostingDate,
        string CurrencyCode,
        Guid ValuedAcceptanceId,
        string AcceptanceNumber,
        Guid GoodsReceiptId,
        Guid PurchaseOrderId,
        Guid SupplierPartnerId,
        string IdempotencyKey,
        IReadOnlyList<ReceiptAccrualLineInput> Lines,
        Guid ActorUserId,
        DateTimeOffset NowUtc);

    public sealed record AccrualAccountResolution(
        bool IsValid,
        Guid InventoryAssetAccountId,
        string InventoryAssetAccountCode,
        string InventoryAssetAccountName,
        Guid GrniAccountId,
        string GrniAccountCode,
        string GrniAccountName,
        string? PeriodCode,
        IReadOnlyList<string> BlockingReasons);

    public sealed record ReceiptAccrualOutcome(
        bool Succeeded,
        int FailureHttpStatus,
        string? ErrorCode,
        string? ErrorDetail,
        JournalEntry? Journal,
        IReadOnlyList<JournalLine> JournalLines,
        ReceiptAccrual? Accrual,
        IReadOnlyList<ReceiptAccrualLine> AccrualLines,
        string? PeriodCode)
    {
        public static ReceiptAccrualOutcome Fail(int statusCode, string errorCode, string detail) =>
            new(false, statusCode, errorCode, detail, null, Array.Empty<JournalLine>(), null, Array.Empty<ReceiptAccrualLine>(), null);
    }

    public static async Task<AccrualAccountResolution> PreviewPeriodAndAccrualMappingsAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        DateOnly postingDate,
        CancellationToken ct)
    {
        var blocks = new List<string>();

        var period = await db.FinancialPeriods.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.StartDate <= postingDate && p.EndDate >= postingDate)
            .OrderBy(p => p.PeriodNumber)
            .FirstOrDefaultAsync(ct);

        if (period is null)
        {
            blocks.Add($"period_not_found_for_date: No fiscal period configured for {postingDate:yyyy-MM-dd}.");
        }
        else if (!period.IsOpenForPosting(postingDate))
        {
            blocks.Add($"period_closed: Fiscal period '{period.PeriodCode}' is '{period.Status}' and refuses postings.");
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId &&
                m.LegalEntityId == entityId &&
                (m.PurposeCode == GlMappingPurposes.InventoryAsset || m.PurposeCode == GlMappingPurposes.GrniClearing))
            .ToDictionaryAsync(m => m.PurposeCode, ct);

        Guid invAccountId = Guid.Empty;
        string invCode = string.Empty;
        string invName = string.Empty;
        Guid grniAccountId = Guid.Empty;
        string grniCode = string.Empty;
        string grniName = string.Empty;

        var accountIds = mappings.Values.Select(m => m.AccountId).Distinct().ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, GlAccount>()
            : await db.GlAccounts.AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.LegalEntityId == entityId && accountIds.Contains(a.AccountId))
                .ToDictionaryAsync(a => a.AccountId, ct);

        if (!mappings.TryGetValue(GlMappingPurposes.InventoryAsset, out var invMap) ||
            !accounts.TryGetValue(invMap.AccountId, out var invAcc))
        {
            blocks.Add("missing_inventory_asset_mapping: Configure an active Inventory Asset account mapping in Finance.");
        }
        else
        {
            invAccountId = invAcc.AccountId;
            invCode = invAcc.AccountCode;
            invName = invAcc.Name;
            if (invAcc.Status != MasterStatuses.Active || !invAcc.AllowPosting)
            {
                blocks.Add($"account_not_postable: Inventory Asset account '{invAcc.AccountCode}' must be active and postable.");
            }
        }

        if (!mappings.TryGetValue(GlMappingPurposes.GrniClearing, out var grniMap) ||
            !accounts.TryGetValue(grniMap.AccountId, out var grniAcc))
        {
            blocks.Add("missing_grni_clearing_mapping: Configure an active GRNI Clearing account mapping in Finance.");
        }
        else
        {
            grniAccountId = grniAcc.AccountId;
            grniCode = grniAcc.AccountCode;
            grniName = grniAcc.Name;
            if (grniAcc.Status != MasterStatuses.Active || !grniAcc.AllowPosting)
            {
                blocks.Add($"account_not_postable: GRNI Clearing account '{grniAcc.AccountCode}' must be active and postable.");
            }
        }

        return new AccrualAccountResolution(
            blocks.Count == 0,
            invAccountId,
            invCode,
            invName,
            grniAccountId,
            grniCode,
            grniName,
            period?.PeriodCode,
            blocks);
    }

    public static async Task<ReceiptAccrualOutcome> PostReceiptAccrualAsync(
        AtheraDbContext db,
        PostReceiptAccrualCommand cmd,
        CancellationToken ct)
    {
        if (cmd.Lines is null || cmd.Lines.Count == 0)
        {
            return ReceiptAccrualOutcome.Fail(400, "acceptance_lines_required", "At least one valued acceptance line is required.");
        }

        var mappings = await db.AccountMappings.AsNoTracking()
            .Where(m =>
                m.TenantId == cmd.TenantId &&
                m.LegalEntityId == cmd.LegalEntityId &&
                (m.PurposeCode == GlMappingPurposes.InventoryAsset || m.PurposeCode == GlMappingPurposes.GrniClearing))
            .ToDictionaryAsync(m => m.PurposeCode, ct);

        if (!mappings.TryGetValue(GlMappingPurposes.InventoryAsset, out var invMap))
        {
            return ReceiptAccrualOutcome.Fail(
                409,
                "missing_inventory_asset_mapping",
                "INVENTORY_ASSET GL account mapping is missing for this legal entity.");
        }

        if (!mappings.TryGetValue(GlMappingPurposes.GrniClearing, out var grniMap))
        {
            return ReceiptAccrualOutcome.Fail(
                409,
                "missing_grni_clearing_mapping",
                "GRNI_CLEARING GL account mapping is missing for this legal entity.");
        }

        var totalAccrued = decimal.Round(cmd.Lines.Sum(l => l.AcceptedLineAmount), 4, MidpointRounding.AwayFromZero);
        if (totalAccrued <= 0m)
        {
            return ReceiptAccrualOutcome.Fail(
                409,
                "od04_zero_or_negative_acceptance_value_blocked",
                "Valued acceptance requires a strictly positive accrued monetary amount under OD-04.");
        }

        var glLines = new List<GlJournalLineInput>(cmd.Lines.Count + 1);
        foreach (var line in cmd.Lines)
        {
            glLines.Add(new GlJournalLineInput(
                invMap.AccountId,
                $"Dr Inventory {line.ItemCode} ({line.AcceptedBaseQuantity:0.######}) · {cmd.AcceptanceNumber}",
                DebitAmount: decimal.Round(line.AcceptedLineAmount, 4, MidpointRounding.AwayFromZero),
                CreditAmount: 0m,
                PartnerId: cmd.SupplierPartnerId,
                ItemId: line.ItemId));
        }

        glLines.Add(new GlJournalLineInput(
            grniMap.AccountId,
            $"Cr GRNI Accrual · {cmd.AcceptanceNumber}",
            DebitAmount: 0m,
            CreditAmount: totalAccrued,
            PartnerId: cmd.SupplierPartnerId));

        var rawJournalKey = $"acc:{cmd.GoodsReceiptId:N}:{cmd.IdempotencyKey.Trim()}";
        var journalIdempotencyKey = rawJournalKey.Length <= 80
            ? rawJournalKey
            : $"acc:{cmd.GoodsReceiptId:N}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cmd.IdempotencyKey.Trim())))[..32].ToLowerInvariant()}";

        var journalOutcome = await PostJournalAsync(
            db,
            new GlPostJournalCommand(
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ExpectedFunctionalCurrencyCode,
                cmd.PostingDate,
                cmd.CurrencyCode,
                $"Valued Receipt {cmd.AcceptanceNumber} (Dr Inventory / Cr GRNI)",
                "PROCUREMENT",
                "VALUED_ACCEPTANCE",
                cmd.AcceptanceNumber,
                journalIdempotencyKey,
                glLines,
                cmd.ActorUserId,
                cmd.NowUtc),
            ct);

        if (!journalOutcome.Succeeded || journalOutcome.Journal is null)
        {
            return ReceiptAccrualOutcome.Fail(
                journalOutcome.FailureHttpStatus,
                journalOutcome.ErrorCode ?? "gl_posting_failed",
                journalOutcome.ErrorDetail ?? "Failed posting Inventory / GRNI journal.");
        }

        var existingAccrual = await db.ReceiptAccruals.AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.TenantId == cmd.TenantId && a.LegalEntityId == cmd.LegalEntityId && a.ValuedAcceptanceId == cmd.ValuedAcceptanceId,
                ct);

        if (existingAccrual is not null)
        {
            var existingLines = await db.ReceiptAccrualLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.ReceiptAccrualId == existingAccrual.ReceiptAccrualId)
                .OrderBy(l => l.LineNumber)
                .ToListAsync(ct);

            return new ReceiptAccrualOutcome(
                true,
                200,
                null,
                null,
                journalOutcome.Journal,
                journalOutcome.Lines,
                existingAccrual,
                existingLines,
                journalOutcome.PeriodCode);
        }

        var accrualId = Guid.NewGuid();
        var accrualNumber = cmd.AcceptanceNumber.StartsWith("ACC-", StringComparison.Ordinal)
            ? $"GRNI-{cmd.AcceptanceNumber[4..]}"
            : $"GRNI-{cmd.AcceptanceNumber}";

        var accrual = ReceiptAccrual.Create(
            accrualId,
            cmd.TenantId,
            cmd.LegalEntityId,
            accrualNumber,
            cmd.ValuedAcceptanceId,
            cmd.GoodsReceiptId,
            cmd.PurchaseOrderId,
            cmd.SupplierPartnerId,
            journalOutcome.Journal.JournalId,
            invMap.AccountId,
            grniMap.AccountId,
            cmd.PostingDate,
            cmd.CurrencyCode,
            totalAccrued,
            cmd.ActorUserId,
            cmd.NowUtc);

        var accrualLines = cmd.Lines
            .Select(l => ReceiptAccrualLine.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                accrualId,
                l.LineNumber,
                l.ValuedAcceptanceLineId,
                l.GoodsReceiptLineId,
                l.PoLineId,
                l.ItemId,
                l.AcceptedBaseQuantity,
                l.UnitCostBase,
                l.AcceptedLineAmount))
            .ToList();

        db.ReceiptAccruals.Add(accrual);
        db.ReceiptAccrualLines.AddRange(accrualLines);

        return new ReceiptAccrualOutcome(
            true,
            201,
            null,
            null,
            journalOutcome.Journal,
            journalOutcome.Lines,
            accrual,
            accrualLines,
            journalOutcome.PeriodCode);
    }
}
