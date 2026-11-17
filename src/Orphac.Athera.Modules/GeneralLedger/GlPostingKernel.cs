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
}
