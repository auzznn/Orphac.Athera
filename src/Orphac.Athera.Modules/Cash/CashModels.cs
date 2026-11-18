using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Cash;

public static class CashPolicyDefaults
{
    public const string SupplierPaymentReleasePolicyCode = "SUPPLIER_PAYMENT_RELEASE";
}

public static class PaymentMethods
{
    public const string BankTransferIdr = "BANK_TRANSFER_IDR";
    public const string ClearingRtgs = "CLEARING_RTGS";
    public const string BifastIdr = "BIFAST_IDR";

    public static readonly IReadOnlyList<string> All = [BankTransferIdr, ClearingRtgs, BifastIdr];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? BankTransferIdr : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported payment method '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class SupplierPaymentStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingRelease = "PENDING_RELEASE";
    public const string Released = "RELEASED";
    public const string PartiallyReconciled = "PARTIALLY_RECONCILED";
    public const string Reconciled = "RECONCILED";
    public const string BankRejected = "BANK_REJECTED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All =
    [
        Draft, PendingRelease, Released, PartiallyReconciled, Reconciled, BankRejected, Cancelled
    ];
}

public static class PaymentInstructionFormats
{
    public const string SimulatedCsvV1 = "ATH_CSV_SIM_V1";

    public static readonly IReadOnlyList<string> All = [SimulatedCsvV1];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? SimulatedCsvV1 : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported simulated instruction format '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class BankStatementLineDirections
{
    public const string Debit = "DEBIT";
    public const string Credit = "CREDIT";

    public static readonly IReadOnlyList<string> All = [Debit, Credit];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported bank statement line direction '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class BankStatementLineStatuses
{
    public const string Unmatched = "UNMATCHED";
    public const string PartiallyMatched = "PARTIALLY_MATCHED";
    public const string Matched = "MATCHED";
    public const string Ignored = "IGNORED";

    public static readonly IReadOnlyList<string> All = [Unmatched, PartiallyMatched, Matched, Ignored];
}

public static class BankPaymentMatchStatuses
{
    public const string Posted = "POSTED";
    public const string Reversed = "REVERSED";

    public static readonly IReadOnlyList<string> All = [Posted, Reversed];
}

public sealed class BankAccount
{
    public Guid BankAccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string AccountCode { get; private set; } = string.Empty;
    public string BankName { get; private set; } = string.Empty;
    public string? BranchName { get; private set; }
    public string AccountNumber { get; private set; } = string.Empty;
    public string AccountHolderName { get; private set; } = string.Empty;
    public string CurrencyCode { get; private set; } = "IDR";
    public Guid GlAccountId { get; private set; }
    public Guid ClearingGlAccountId { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private BankAccount()
    {
    }

    public static BankAccount Create(
        Guid bankAccountId,
        Guid tenantId,
        Guid legalEntityId,
        string accountCode,
        string bankName,
        string? branchName,
        string accountNumber,
        string accountHolderName,
        string currencyCode,
        Guid glAccountId,
        Guid clearingGlAccountId,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(bankAccountId, nameof(bankAccountId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(glAccountId, nameof(glAccountId));
        GuardValidation.RequireNonEmptyGuid(clearingGlAccountId, nameof(clearingGlAccountId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        return new BankAccount
        {
            BankAccountId = bankAccountId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            AccountCode = GuardValidation.RequireMasterCode(accountCode, nameof(accountCode), 2, 32),
            BankName = GuardValidation.RequireName(bankName, nameof(bankName), 160),
            BranchName = GuardValidation.NormalizeOptional(branchName, 120, nameof(branchName)),
            AccountNumber = GuardValidation.RequireName(accountNumber, nameof(accountNumber), 64),
            AccountHolderName = GuardValidation.RequireName(accountHolderName, nameof(accountHolderName), 160),
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            GlAccountId = glAccountId,
            ClearingGlAccountId = clearingGlAccountId,
            Status = MasterStatuses.Active,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDetails(
        string bankName,
        string? branchName,
        string accountNumber,
        string accountHolderName,
        Guid glAccountId,
        Guid clearingGlAccountId,
        string status,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(glAccountId, nameof(glAccountId));
        GuardValidation.RequireNonEmptyGuid(clearingGlAccountId, nameof(clearingGlAccountId));

        BankName = GuardValidation.RequireName(bankName, nameof(bankName), 160);
        BranchName = GuardValidation.NormalizeOptional(branchName, 120, nameof(branchName));
        AccountNumber = GuardValidation.RequireName(accountNumber, nameof(accountNumber), 64);
        AccountHolderName = GuardValidation.RequireName(accountHolderName, nameof(accountHolderName), 160);
        GlAccountId = glAccountId;
        ClearingGlAccountId = clearingGlAccountId;
        Status = MasterStatuses.Normalize(status);
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class InstructionSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private InstructionSequence()
    {
    }
}

public sealed class StatementSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private StatementSequence()
    {
    }
}

public sealed class MatchSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public int FiscalYear { get; private set; }
    public long LastNumber { get; private set; }

    private MatchSequence()
    {
    }
}

/// <summary>
/// Append-only non-live payment instruction file simulation (`SimulationOnly = true`).
/// File generation never posts to GL or reduces supplier AP balances (`OD-06`).
/// </summary>
public sealed class PaymentInstruction
{
    public Guid PaymentInstructionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string InstructionNumber { get; private set; } = string.Empty;
    public Guid SupplierPaymentId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public string FormatCode { get; private set; } = PaymentInstructionFormats.SimulatedCsvV1;
    public bool SimulationOnly { get; private set; } = true;
    public decimal TotalAmount { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public string PayloadText { get; private set; } = string.Empty;
    public string PayloadSha256 { get; private set; } = string.Empty;
    public Guid GeneratedByUserId { get; private set; }
    public DateTimeOffset GeneratedAtUtc { get; private set; }

    private PaymentInstruction()
    {
    }

    public static PaymentInstruction RecordSimulation(
        Guid instructionId,
        Guid tenantId,
        Guid legalEntityId,
        string instructionNumber,
        Guid supplierPaymentId,
        Guid bankAccountId,
        string formatCode,
        decimal totalAmount,
        string currencyCode,
        string payloadText,
        Guid generatedByUserId,
        DateTimeOffset generatedAtUtc)
    {
        GuardValidation.RequireNonEmptyGuid(instructionId, nameof(instructionId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierPaymentId, nameof(supplierPaymentId));
        GuardValidation.RequireNonEmptyGuid(bankAccountId, nameof(bankAccountId));
        GuardValidation.RequireNonEmptyGuid(generatedByUserId, nameof(generatedByUserId));

        var amt = decimal.Round(totalAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Instruction amount must be strictly positive.");
        }

        if (string.IsNullOrWhiteSpace(payloadText))
        {
            throw new ArgumentException("Simulated instruction payload cannot be empty.", nameof(payloadText));
        }

        var trimmedPayload = payloadText.Length > 16000 ? payloadText[..16000] : payloadText;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(trimmedPayload)));

        return new PaymentInstruction
        {
            PaymentInstructionId = instructionId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            InstructionNumber = GuardValidation.RequireCode(instructionNumber, nameof(instructionNumber), 40),
            SupplierPaymentId = supplierPaymentId,
            BankAccountId = bankAccountId,
            FormatCode = PaymentInstructionFormats.Normalize(formatCode),
            SimulationOnly = true,
            TotalAmount = amt,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            PayloadText = trimmedPayload,
            PayloadSha256 = hash,
            GeneratedByUserId = generatedByUserId,
            GeneratedAtUtc = generatedAtUtc
        };
    }
}

public sealed class BankStatement
{
    public Guid BankStatementId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string StatementNumber { get; private set; } = string.Empty;
    public Guid BankAccountId { get; private set; }
    public string BankAccountCodeSnapshot { get; private set; } = string.Empty;
    public string StatementReference { get; private set; } = string.Empty;
    public DateOnly StatementDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal OpeningBalance { get; private set; }
    public decimal ClosingBalance { get; private set; }
    public decimal TotalDebitAmount { get; private set; }
    public decimal TotalCreditAmount { get; private set; }
    public int LineCount { get; private set; }
    public string SourceFormat { get; private set; } = "CSV_UPLOAD";
    public string PayloadSha256 { get; private set; } = string.Empty;
    public Guid ImportedByUserId { get; private set; }
    public DateTimeOffset ImportedAtUtc { get; private set; }

    private BankStatement()
    {
    }

    public static BankStatement RecordImport(
        Guid statementId,
        Guid tenantId,
        Guid legalEntityId,
        string statementNumber,
        BankAccount bankAccount,
        string statementReference,
        DateOnly statementDate,
        decimal openingBalance,
        decimal closingBalance,
        decimal totalDebitAmount,
        decimal totalCreditAmount,
        int lineCount,
        string sourceFormat,
        string rawPayload,
        Guid importedByUserId,
        DateTimeOffset importedAtUtc)
    {
        GuardValidation.RequireNonEmptyGuid(statementId, nameof(statementId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(importedByUserId, nameof(importedByUserId));
        ArgumentNullException.ThrowIfNull(bankAccount);
        if (lineCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineCount), "Bank statement must contain at least one line.");
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(sourceFormat) ? "CSV_UPLOAD" : sourceFormat.Trim().ToUpperInvariant();
        if (normalizedFormat is not ("CSV_UPLOAD" or "MANUAL_STAGE"))
        {
            throw new ArgumentException($"Unsupported statement source format '{sourceFormat}'.", nameof(sourceFormat));
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload ?? string.Empty)));

        return new BankStatement
        {
            BankStatementId = statementId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            StatementNumber = GuardValidation.RequireCode(statementNumber, nameof(statementNumber), 40),
            BankAccountId = bankAccount.BankAccountId,
            BankAccountCodeSnapshot = bankAccount.AccountCode,
            StatementReference = GuardValidation.RequireName(statementReference, nameof(statementReference), 100),
            StatementDate = statementDate,
            CurrencyCode = bankAccount.CurrencyCode,
            OpeningBalance = decimal.Round(openingBalance, 4, MidpointRounding.AwayFromZero),
            ClosingBalance = decimal.Round(closingBalance, 4, MidpointRounding.AwayFromZero),
            TotalDebitAmount = decimal.Round(totalDebitAmount, 4, MidpointRounding.AwayFromZero),
            TotalCreditAmount = decimal.Round(totalCreditAmount, 4, MidpointRounding.AwayFromZero),
            LineCount = lineCount,
            SourceFormat = normalizedFormat,
            PayloadSha256 = hash,
            ImportedByUserId = importedByUserId,
            ImportedAtUtc = importedAtUtc
        };
    }
}

public sealed class BankStatementLine
{
    public Guid BankStatementLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid BankStatementId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public int LineNumber { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public string Direction { get; private set; } = BankStatementLineDirections.Debit;
    public decimal Amount { get; private set; }
    public decimal MatchedAmount { get; private set; }
    public decimal UnmatchedAmount { get; private set; }
    public string BankReference { get; private set; } = string.Empty;
    public string? CounterpartyName { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string LineFingerprintSha256 { get; private set; } = string.Empty;
    public string Status { get; private set; } = BankStatementLineStatuses.Unmatched;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private BankStatementLine()
    {
    }

    public static string ComputeFingerprintSha256(
        Guid bankAccountId,
        DateOnly valueDate,
        string direction,
        decimal amount,
        string bankReference)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{bankAccountId:N}|{valueDate:yyyy-MM-dd}|{BankStatementLineDirections.Normalize(direction)}|{decimal.Round(amount, 4, MidpointRounding.AwayFromZero):F4}|{bankReference.Trim().ToUpperInvariant()}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static BankStatementLine Create(
        Guid lineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid bankStatementId,
        Guid bankAccountId,
        int lineNumber,
        DateOnly valueDate,
        string direction,
        decimal amount,
        string bankReference,
        string? counterpartyName,
        string description,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(lineId, nameof(lineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(bankStatementId, nameof(bankStatementId));
        GuardValidation.RequireNonEmptyGuid(bankAccountId, nameof(bankAccountId));
        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        }

        var normalizedDir = BankStatementLineDirections.Normalize(direction);
        var amt = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Statement line amount must be strictly positive.");
        }

        var cleanRef = GuardValidation.RequireName(bankReference, nameof(bankReference), 100);
        var fingerprint = ComputeFingerprintSha256(bankAccountId, valueDate, normalizedDir, amt, cleanRef);

        return new BankStatementLine
        {
            BankStatementLineId = lineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            BankStatementId = bankStatementId,
            BankAccountId = bankAccountId,
            LineNumber = lineNumber,
            ValueDate = valueDate,
            Direction = normalizedDir,
            Amount = amt,
            MatchedAmount = 0m,
            UnmatchedAmount = amt,
            BankReference = cleanRef,
            CounterpartyName = GuardValidation.NormalizeOptional(counterpartyName, 160, nameof(counterpartyName)),
            Description = GuardValidation.RequireName(description, nameof(description), 300),
            LineFingerprintSha256 = fingerprint,
            Status = BankStatementLineStatuses.Unmatched,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyMatch(decimal matchAmount, DateTimeOffset nowUtc)
    {
        if (Direction != BankStatementLineDirections.Debit)
        {
            throw new InvalidOperationException("statement_line_direction_invalid: Outbound supplier payment reconciliation requires a DEBIT bank statement line.");
        }

        if (Status == BankStatementLineStatuses.Matched || Status == BankStatementLineStatuses.Ignored)
        {
            throw new InvalidOperationException($"statement_line_already_settled: Bank statement line '{BankReference}' is already '{Status}'.");
        }

        var amt = decimal.Round(matchAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(matchAmount), "Match amount must be strictly positive.");
        }

        if (amt > UnmatchedAmount)
        {
            throw new InvalidOperationException(
                $"match_exceeds_unmatched_statement_line: Match amount ({amt:F4}) exceeds unmatched bank statement balance ({UnmatchedAmount:F4}).");
        }

        MatchedAmount = decimal.Round(MatchedAmount + amt, 4, MidpointRounding.AwayFromZero);
        UnmatchedAmount = decimal.Round(Amount - MatchedAmount, 4, MidpointRounding.AwayFromZero);
        if (UnmatchedAmount < 0m || Amount != MatchedAmount + UnmatchedAmount)
        {
            throw new InvalidOperationException("statement_line_conservation_failed");
        }

        Status = UnmatchedAmount == 0m
            ? BankStatementLineStatuses.Matched
            : BankStatementLineStatuses.PartiallyMatched;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void ReverseMatch(decimal reversalAmount, DateTimeOffset nowUtc)
    {
        var amt = decimal.Round(reversalAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m || amt > MatchedAmount)
        {
            throw new InvalidOperationException("reversal_exceeds_matched_statement_amount");
        }

        MatchedAmount = decimal.Round(MatchedAmount - amt, 4, MidpointRounding.AwayFromZero);
        UnmatchedAmount = decimal.Round(Amount - MatchedAmount, 4, MidpointRounding.AwayFromZero);
        Status = MatchedAmount == 0m
            ? BankStatementLineStatuses.Unmatched
            : BankStatementLineStatuses.PartiallyMatched;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class BankPaymentMatch
{
    public Guid BankPaymentMatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string MatchNumber { get; private set; } = string.Empty;
    public Guid BankAccountId { get; private set; }
    public Guid BankStatementId { get; private set; }
    public Guid BankStatementLineId { get; private set; }
    public Guid SupplierPaymentId { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public decimal MatchedAmount { get; private set; }
    public string Status { get; private set; } = BankPaymentMatchStatuses.Posted;
    public string MatchIdempotencyKey { get; private set; } = string.Empty;
    public Guid ClearingGlJournalId { get; private set; }
    public Guid? ReversalGlJournalId { get; private set; }
    public string? ReversalReason { get; private set; }
    public Guid MatchedByUserId { get; private set; }
    public DateTimeOffset MatchedAtUtc { get; private set; }
    public Guid? ReversedByUserId { get; private set; }
    public DateTimeOffset? ReversedAtUtc { get; private set; }
    public long Version { get; private set; }

    private BankPaymentMatch()
    {
    }

    public static BankPaymentMatch Record(
        Guid matchId,
        Guid tenantId,
        Guid legalEntityId,
        string matchNumber,
        BankStatementLine line,
        Guid supplierPaymentId,
        DateOnly postingDate,
        decimal matchedAmount,
        string matchIdempotencyKey,
        Guid clearingGlJournalId,
        Guid matchedByUserId,
        DateTimeOffset matchedAtUtc)
    {
        GuardValidation.RequireNonEmptyGuid(matchId, nameof(matchId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(supplierPaymentId, nameof(supplierPaymentId));
        GuardValidation.RequireNonEmptyGuid(clearingGlJournalId, nameof(clearingGlJournalId));
        GuardValidation.RequireNonEmptyGuid(matchedByUserId, nameof(matchedByUserId));
        ArgumentNullException.ThrowIfNull(line);

        var amt = decimal.Round(matchedAmount, 4, MidpointRounding.AwayFromZero);
        if (amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(matchedAmount));
        }

        return new BankPaymentMatch
        {
            BankPaymentMatchId = matchId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            MatchNumber = GuardValidation.RequireCode(matchNumber, nameof(matchNumber), 40),
            BankAccountId = line.BankAccountId,
            BankStatementId = line.BankStatementId,
            BankStatementLineId = line.BankStatementLineId,
            SupplierPaymentId = supplierPaymentId,
            PostingDate = postingDate,
            MatchedAmount = amt,
            Status = BankPaymentMatchStatuses.Posted,
            MatchIdempotencyKey = GuardValidation.RequireName(matchIdempotencyKey, nameof(matchIdempotencyKey), 80),
            ClearingGlJournalId = clearingGlJournalId,
            MatchedByUserId = matchedByUserId,
            MatchedAtUtc = matchedAtUtc,
            Version = 1L
        };
    }

    public void MarkReversed(Guid reversalGlJournalId, string reason, Guid actorUserId, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(reversalGlJournalId, nameof(reversalGlJournalId));
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != BankPaymentMatchStatuses.Posted)
        {
            throw new InvalidOperationException($"match_already_reversed: Bank match '{MatchNumber}' is already '{Status}'.");
        }

        Status = BankPaymentMatchStatuses.Reversed;
        ReversalGlJournalId = reversalGlJournalId;
        ReversalReason = GuardValidation.RequireName(reason, nameof(reason), 300);
        ReversedByUserId = actorUserId;
        ReversedAtUtc = nowUtc;
        Version++;
    }
}
