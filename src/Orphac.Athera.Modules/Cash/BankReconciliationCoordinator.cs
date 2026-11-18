using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Cash;

public sealed record BankStatementLineInput(
    DateOnly ValueDate,
    string Direction,
    decimal Amount,
    string BankReference,
    string? CounterpartyName,
    string Description);

public sealed record ImportBankStatementCommand(
    Guid TenantId,
    Guid LegalEntityId,
    Guid BankAccountId,
    string StatementReference,
    DateOnly StatementDate,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? CsvContent,
    IReadOnlyList<BankStatementLineInput>? Lines,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record BankStatementImportOutcome(
    bool Succeeded,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    BankStatement? Statement,
    IReadOnlyList<BankStatementLine> Lines)
{
    public static BankStatementImportOutcome Fail(int status, string code, string detail) =>
        new(false, status, code, detail, null, Array.Empty<BankStatementLine>());

    public static BankStatementImportOutcome Success(BankStatement statement, IReadOnlyList<BankStatementLine> lines) =>
        new(true, 201, null, null, statement, lines);
}

public sealed record ReconcileBankLineCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid BankStatementLineId,
    Guid SupplierPaymentId,
    DateOnly PostingDate,
    decimal? MatchedAmount,
    string IdempotencyKey,
    Guid ActorUserId,
    DateTimeOffset NowUtc,
    Func<Task>? OnAfterGlPostedBeforeCommitAsync = null);

public sealed record ReverseBankMatchCommand(
    Guid TenantId,
    Guid LegalEntityId,
    string ExpectedFunctionalCurrencyCode,
    Guid BankPaymentMatchId,
    DateOnly ReversalPostingDate,
    string Reason,
    Guid ActorUserId,
    DateTimeOffset NowUtc);

public sealed record BankReconciliationOutcome(
    bool Succeeded,
    bool IdempotentReplay,
    int FailureHttpStatus,
    string? ErrorCode,
    string? ErrorDetail,
    BankPaymentMatch? Match,
    BankStatementLine? StatementLine,
    SupplierPayment? Payment,
    JournalEntry? Journal)
{
    public static BankReconciliationOutcome Fail(int status, string code, string detail) =>
        new(false, false, status, code, detail, null, null, null, null);

    public static BankReconciliationOutcome Success(
        BankPaymentMatch match,
        BankStatementLine line,
        SupplierPayment payment,
        JournalEntry? journal,
        bool replay = false) =>
        new(true, replay, replay ? 200 : 201, null, null, match, line, payment, journal);
}

public static class BankReconciliationCoordinator
{
    public static IReadOnlyList<BankStatementLineInput> ParseCsvLines(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return Array.Empty<BankStatementLineInput>();
        }

        var rows = csvContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<BankStatementLineInput>();

        foreach (var rawRow in rows)
        {
            if (rawRow.StartsWith('#'))
            {
                continue;
            }

            var cols = rawRow.Split(',', StringSplitOptions.TrimEntries);
            if (cols.Length < 4)
            {
                throw new ArgumentException($"Invalid CSV bank statement row '{rawRow}'. Expected: ValueDate,Direction,Amount,BankReference[,Counterparty,Description].");
            }

            if (cols[0].Equals("ValueDate", StringComparison.OrdinalIgnoreCase) ||
                cols[0].Equals("Date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DateOnly.TryParseExact(cols[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate))
            {
                throw new ArgumentException($"Invalid ValueDate '{cols[0]}'. Expected yyyy-MM-dd.");
            }

            if (!decimal.TryParse(cols[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                throw new ArgumentException($"Invalid statement amount '{cols[2]}'.");
            }

            var counterparty = cols.Length > 4 ? cols[4] : null;
            var description = cols.Length > 5 ? string.Join(", ", cols.Skip(5)) : $"{cols[1].ToUpperInvariant()} {cols[3]}";

            results.Add(new BankStatementLineInput(valueDate, cols[1], amount, cols[3], counterparty, description));
        }

        return results;
    }

    public static async Task<BankStatementImportOutcome> ImportBankStatementAsync(
        AtheraDbContext db,
        ImportBankStatementCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("ImportBankStatementAsync requires active entity transaction.");
        const string sp = "sp_import_bank_statement";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            var bankAccount = await db.BankAccounts.SingleOrDefaultAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == cmd.BankAccountId,
                ct);
            if (bankAccount is null || bankAccount.Status != MasterStatuses.Active)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankStatementImportOutcome.Fail(400, "invalid_bank_account", "Active bank account was not found in this legal entity.");
            }

            var cleanRef = GuardValidation.RequireName(cmd.StatementReference, nameof(cmd.StatementReference), 100);
            var duplicateStatement = await db.BankStatements.AsNoTracking().AnyAsync(
                s => s.TenantId == cmd.TenantId &&
                     s.LegalEntityId == cmd.LegalEntityId &&
                     s.BankAccountId == bankAccount.BankAccountId &&
                     s.StatementReference == cleanRef,
                ct);
            if (duplicateStatement)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankStatementImportOutcome.Fail(
                    409,
                    "duplicate_bank_statement_reference",
                    $"Bank statement reference '{cleanRef}' was already imported for bank account '{bankAccount.AccountCode}'.");
            }

            var parsedInputs = cmd.Lines is { Count: > 0 }
                ? cmd.Lines
                : ParseCsvLines(cmd.CsvContent ?? string.Empty);
            if (parsedInputs.Count == 0)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankStatementImportOutcome.Fail(400, "statement_lines_required", "Provide at least one valid bank statement line or CSV row.");
            }

            var statementId = Guid.NewGuid();
            var lines = new List<BankStatementLine>(parsedInputs.Count);
            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            decimal debitSum = 0m;
            decimal creditSum = 0m;

            for (var i = 0; i < parsedInputs.Count; i++)
            {
                var input = parsedInputs[i];
                var line = BankStatementLine.Create(
                    Guid.NewGuid(),
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    statementId,
                    bankAccount.BankAccountId,
                    i + 1,
                    input.ValueDate,
                    input.Direction,
                    input.Amount,
                    input.BankReference,
                    input.CounterpartyName,
                    input.Description,
                    cmd.NowUtc);

                if (!fingerprints.Add(line.LineFingerprintSha256))
                {
                    await tx.RollbackToSavepointAsync(sp, ct);
                    return BankStatementImportOutcome.Fail(
                        409,
                        "duplicate_bank_statement_line",
                        $"Statement contains duplicate transaction fingerprint for reference '{line.BankReference}'.");
                }

                if (line.Direction == BankStatementLineDirections.Debit) debitSum += line.Amount;
                else creditSum += line.Amount;

                lines.Add(line);
            }

            var existingFingerprints = await db.BankStatementLines.AsNoTracking()
                .Where(l => l.TenantId == cmd.TenantId &&
                            l.LegalEntityId == cmd.LegalEntityId &&
                            l.BankAccountId == bankAccount.BankAccountId &&
                            fingerprints.Contains(l.LineFingerprintSha256))
                .Select(l => l.BankReference)
                .ToListAsync(ct);

            if (existingFingerprints.Count > 0)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankStatementImportOutcome.Fail(
                    409,
                    "duplicate_bank_statement_line",
                    $"Duplicate statement transaction detected (reference '{existingFingerprints[0]}') — previously imported line cannot be re-imported to settle twice.");
            }

            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "cash.statement_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.StatementDate.Year, ct);
            var statementNumber = $"BST-{cmd.StatementDate.Year:D4}-{seq:D5}";

            var rawPayload = !string.IsNullOrWhiteSpace(cmd.CsvContent)
                ? cmd.CsvContent
                : JsonSerializer.Serialize(parsedInputs);
            var sourceFormat = !string.IsNullOrWhiteSpace(cmd.CsvContent) ? "CSV_UPLOAD" : "MANUAL_STAGE";

            var statement = BankStatement.RecordImport(
                statementId,
                cmd.TenantId,
                cmd.LegalEntityId,
                statementNumber,
                bankAccount,
                cleanRef,
                cmd.StatementDate,
                cmd.OpeningBalance,
                cmd.ClosingBalance,
                debitSum,
                creditSum,
                lines.Count,
                sourceFormat,
                rawPayload,
                cmd.ActorUserId,
                cmd.NowUtc);

            db.BankStatements.Add(statement);
            db.BankStatementLines.AddRange(lines);

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_BANK_STATEMENT_IMPORTED",
                "BANK_STATEMENT",
                statement.BankStatementId.ToString("D"),
                $"Imported statement {statement.StatementNumber} ({cleanRef}) for {bankAccount.AccountCode}: {lines.Count} line(s), DEBIT={debitSum:N0} IDR",
                JsonSerializer.Serialize(new { statement.BankStatementId, statement.StatementNumber, cleanRef, Lines = lines.Count, DebitSum = debitSum }),
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync(sp, ct);
            return BankStatementImportOutcome.Success(statement, lines);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return BankStatementImportOutcome.Fail(400, "statement_validation_failed", ex.Message);
        }
    }

    public static async Task<BankReconciliationOutcome> ReconcileStatementLineAsync(
        AtheraDbContext db,
        ReconcileBankLineCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("ReconcileStatementLineAsync requires active entity transaction.");
        const string sp = "sp_reconcile_bank_line";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            var idempKey = GuardValidation.RequireName(cmd.IdempotencyKey, nameof(cmd.IdempotencyKey), 80);

            var existingMatch = await db.BankPaymentMatches.SingleOrDefaultAsync(
                m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId && m.MatchIdempotencyKey == idempKey,
                ct);
            if (existingMatch is not null)
            {
                var lineSnap = await db.BankStatementLines.AsNoTracking().SingleAsync(
                    l => l.TenantId == cmd.TenantId && l.LegalEntityId == cmd.LegalEntityId && l.BankStatementLineId == existingMatch.BankStatementLineId, ct);
                var paySnap = await db.SupplierPayments.AsNoTracking().SingleAsync(
                    p => p.TenantId == cmd.TenantId && p.LegalEntityId == cmd.LegalEntityId && p.SupplierPaymentId == existingMatch.SupplierPaymentId, ct);
                var jrnSnap = await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(
                    j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.JournalId == existingMatch.ClearingGlJournalId, ct);
                await tx.ReleaseSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Success(existingMatch, lineSnap, paySnap, jrnSnap, replay: true);
            }

            var line = (await db.BankStatementLines
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM cash.bank_statement_lines
                    WHERE tenant_id = {cmd.TenantId}
                      AND legal_entity_id = {cmd.LegalEntityId}
                      AND bank_statement_line_id = {cmd.BankStatementLineId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();

            if (line is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(404, "statement_line_not_found", "Bank statement line was not found in this legal entity.");
            }

            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, cmd.SupplierPaymentId, ct);
            if (payment is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(404, "payment_not_found", "Supplier payment was not found in this legal entity.");
            }

            // Re-check idempotency after acquiring row locks so simultaneous duplicate retries return clean replay.
            var concurrentMatch = await db.BankPaymentMatches.SingleOrDefaultAsync(
                m => m.TenantId == cmd.TenantId && m.LegalEntityId == cmd.LegalEntityId && m.MatchIdempotencyKey == idempKey,
                ct);
            if (concurrentMatch is not null)
            {
                var cJrn = await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(
                    j => j.TenantId == cmd.TenantId && j.LegalEntityId == cmd.LegalEntityId && j.JournalId == concurrentMatch.ClearingGlJournalId, ct);
                await tx.ReleaseSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Success(concurrentMatch, line, payment, cJrn, replay: true);
            }

            if (payment.BankAccountId != line.BankAccountId)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(409, "bank_account_mismatch", "Statement line belongs to a different bank account than the released payment.");
            }

            var amountToMatch = cmd.MatchedAmount.HasValue
                ? decimal.Round(cmd.MatchedAmount.Value, 4, MidpointRounding.AwayFromZero)
                : Math.Min(line.UnmatchedAmount, payment.RemainingClearingAmount);

            if (amountToMatch <= 0m)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(409, "statement_line_or_payment_already_settled", "No open unmatched statement balance or outbound clearing balance remains to match.");
            }

            line.ApplyMatch(amountToMatch, cmd.NowUtc);
            payment.ApplyBankReconciliation(amountToMatch, cmd.NowUtc);

            var bankAccount = await db.BankAccounts.AsNoTracking().SingleAsync(
                b => b.TenantId == cmd.TenantId && b.LegalEntityId == cmd.LegalEntityId && b.BankAccountId == payment.BankAccountId, ct);

            var matchId = Guid.NewGuid();
            var seq = await InventoryValuationService.IncrementEntityYearSequenceAsync(
                db, "cash.match_sequences", cmd.TenantId, cmd.LegalEntityId, cmd.PostingDate.Year, ct);
            var matchNumber = $"BMK-{cmd.PostingDate.Year:D4}-{seq:D5}";

            var journalLines = new List<GlJournalLineInput>
            {
                new(bankAccount.ClearingGlAccountId, $"Clear outbound clearing {matchNumber} ({payment.PaymentNumber ?? payment.DraftCode})", DebitAmount: amountToMatch, CreditAmount: 0m, PartnerId: payment.SupplierPartnerId),
                new(bankAccount.GlAccountId, $"Bank settlement {matchNumber} ({bankAccount.AccountCode} Ref:{line.BankReference})", DebitAmount: 0m, CreditAmount: amountToMatch, PartnerId: payment.SupplierPartnerId)
            };

            var jrnOutcome = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    cmd.ExpectedFunctionalCurrencyCode,
                    cmd.PostingDate,
                    bankAccount.CurrencyCode,
                    $"Bank statement match {matchNumber}: {payment.PaymentNumber ?? payment.DraftCode} <-> {line.BankReference}",
                    "CASH",
                    "BANK_PAYMENT_RECONCILIATION",
                    matchId.ToString("D"),
                    $"cash:bank-match:{matchId:D}",
                    journalLines,
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!jrnOutcome.Succeeded || jrnOutcome.Journal is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(jrnOutcome.FailureHttpStatus, jrnOutcome.ErrorCode ?? "gl_posting_failed", jrnOutcome.ErrorDetail ?? "GL bank clearing failed.");
            }

            var match = BankPaymentMatch.Record(
                matchId,
                cmd.TenantId,
                cmd.LegalEntityId,
                matchNumber,
                line,
                payment.SupplierPaymentId,
                cmd.PostingDate,
                amountToMatch,
                idempKey,
                jrnOutcome.Journal.JournalId,
                cmd.ActorUserId,
                cmd.NowUtc);
            db.BankPaymentMatches.Add(match);

            var payload = JsonSerializer.Serialize(new
            {
                match.BankPaymentMatchId,
                match.MatchNumber,
                payment.SupplierPaymentId,
                payment.PaymentNumber,
                line.BankStatementLineId,
                line.BankReference,
                amountToMatch,
                jrnOutcome.Journal.JournalId,
                jrnOutcome.Journal.JournalNumber
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_BANK_PAYMENT_RECONCILED",
                "BANK_PAYMENT_MATCH",
                match.BankPaymentMatchId.ToString("D"),
                $"Reconciled {match.MatchNumber}: {payment.PaymentNumber ?? payment.DraftCode} vs {line.BankReference} ({amountToMatch:N0} {bankAccount.CurrencyCode}) -> {jrnOutcome.Journal.JournalNumber} (Dr 1120 / Cr 1110)",
                payload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "cash.bank_payment.reconciled.v1",
                $"cash:bank-match:{match.BankPaymentMatchId:D}",
                payload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);

            if (cmd.OnAfterGlPostedBeforeCommitAsync is not null)
            {
                await cmd.OnAfterGlPostedBeforeCommitAsync();
            }

            await tx.ReleaseSavepointAsync(sp, ct);
            return BankReconciliationOutcome.Success(match, line, payment, jrnOutcome.Journal);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return BankReconciliationOutcome.Fail(400, "reconcile_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return BankReconciliationOutcome.Fail(status, code, detail);
        }
        catch
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            throw;
        }
    }

    public static async Task<BankReconciliationOutcome> ReverseBankMatchAsync(
        AtheraDbContext db,
        ReverseBankMatchCommand cmd,
        CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("ReverseBankMatchAsync requires active entity transaction.");
        const string sp = "sp_reverse_bank_match";
        await tx.CreateSavepointAsync(sp, ct);

        try
        {
            var match = (await db.BankPaymentMatches
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM cash.bank_payment_matches
                    WHERE tenant_id = {cmd.TenantId}
                      AND legal_entity_id = {cmd.LegalEntityId}
                      AND bank_payment_match_id = {cmd.BankPaymentMatchId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .SingleOrDefault();

            if (match is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(404, "bank_match_not_found", "Bank payment match was not found.");
            }

            if (match.Status != BankPaymentMatchStatuses.Posted)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(409, "match_already_reversed", $"Bank payment match '{match.MatchNumber}' is already '{match.Status}'.");
            }

            var line = (await db.BankStatementLines
                .FromSqlInterpolated(
                    $"""
                    SELECT * FROM cash.bank_statement_lines
                    WHERE tenant_id = {cmd.TenantId}
                      AND legal_entity_id = {cmd.LegalEntityId}
                      AND bank_statement_line_id = {match.BankStatementLineId}
                    FOR UPDATE
                    """)
                .ToListAsync(ct))
                .Single();

            var payment = await CashCoordinatorHelpers.LockPaymentForUpdateAsync(db, cmd.TenantId, cmd.LegalEntityId, match.SupplierPaymentId, ct)
                ?? throw new InvalidOperationException("linked_payment_missing");

            var revOutcome = await GlPostingKernel.ReverseJournalAsync(
                db,
                new GlReverseJournalCommand(
                    cmd.TenantId,
                    cmd.LegalEntityId,
                    cmd.ExpectedFunctionalCurrencyCode,
                    match.ClearingGlJournalId,
                    cmd.ReversalPostingDate,
                    $"Reverse bank match {match.MatchNumber}: {cmd.Reason}",
                    cmd.ActorUserId,
                    cmd.NowUtc),
                ct);

            if (!revOutcome.Succeeded || revOutcome.Journal is null)
            {
                await tx.RollbackToSavepointAsync(sp, ct);
                return BankReconciliationOutcome.Fail(revOutcome.FailureHttpStatus, revOutcome.ErrorCode ?? "gl_reversal_failed", revOutcome.ErrorDetail ?? "GL reversal failed.");
            }

            match.MarkReversed(revOutcome.Journal.JournalId, cmd.Reason, cmd.ActorUserId, cmd.NowUtc);
            line.ReverseMatch(match.MatchedAmount, cmd.NowUtc);
            payment.ReverseBankReconciliation(match.MatchedAmount, cmd.NowUtc);

            var payload = JsonSerializer.Serialize(new
            {
                match.BankPaymentMatchId,
                match.MatchNumber,
                payment.SupplierPaymentId,
                ReversalJournalId = revOutcome.Journal.JournalId,
                revOutcome.Journal.JournalNumber,
                cmd.Reason
            });

            db.AuditEvents.Add(AuditEvent.Append(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                cmd.ActorUserId,
                "CASH_BANK_RECONCILIATION_REVERSED",
                "BANK_PAYMENT_MATCH",
                match.BankPaymentMatchId.ToString("D"),
                $"Reversed bank match {match.MatchNumber} via {revOutcome.Journal.JournalNumber} (Dr 1110 / Cr 1120)",
                payload,
                cmd.NowUtc));

            db.OutboxMessages.Add(OutboxMessage.Create(
                Guid.NewGuid(),
                cmd.TenantId,
                cmd.LegalEntityId,
                "cash.bank_payment.reconciliation_reversed.v1",
                $"cash:bank-match-reverse:{match.BankPaymentMatchId:D}",
                payload,
                cmd.NowUtc));

            await db.SaveChangesAsync(ct);
            await tx.ReleaseSavepointAsync(sp, ct);
            return BankReconciliationOutcome.Success(match, line, payment, revOutcome.Journal);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            return BankReconciliationOutcome.Fail(400, "reconcile_validation_failed", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackToSavepointAsync(sp, ct);
            var (status, code, detail) = CashCoordinatorHelpers.ClassifyDomainError(ex);
            return BankReconciliationOutcome.Fail(status, code, detail);
        }
    }
}
