using System.Security.Cryptography;
using System.Text;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.GeneralLedger;

public static class GlAccountTypes
{
    public const string Asset = "ASSET";
    public const string Liability = "LIABILITY";
    public const string Equity = "EQUITY";
    public const string Revenue = "REVENUE";
    public const string Expense = "EXPENSE";

    public static readonly IReadOnlyList<string> All = [Asset, Liability, Equity, Revenue, Expense];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported GL account type '{raw}'.", nameof(raw));
        }

        return value;
    }

    public static string DeriveNormalBalance(string accountType) =>
        Normalize(accountType) is Asset or Expense
            ? GlNormalBalances.Debit
            : GlNormalBalances.Credit;
}

public static class GlNormalBalances
{
    public const string Debit = "DEBIT";
    public const string Credit = "CREDIT";

    public static readonly IReadOnlyList<string> All = [Debit, Credit];
}

public static class GlAccountCategories
{
    public const string HeaderGroup = "HEADER_GROUP";
    public const string CashBank = "CASH_BANK";
    public const string BankOutboundClearing = "BANK_OUTBOUND_CLEARING";
    public const string AccountsReceivable = "ACCOUNTS_RECEIVABLE";
    public const string InventoryAsset = "INVENTORY_ASSET";
    public const string TaxReceivable = "TAX_RECEIVABLE";
    public const string GrniAccrual = "GRNI_ACCRUAL";
    public const string AccountsPayable = "ACCOUNTS_PAYABLE";
    public const string TaxPayable = "TAX_PAYABLE";
    public const string WithholdingTaxPayable = "WITHHOLDING_TAX_PAYABLE";
    public const string EquityCapital = "EQUITY_CAPITAL";
    public const string SalesRevenue = "SALES_REVENUE";
    public const string CogsExpense = "COGS_EXPENSE";
    public const string PurchasePriceVariance = "PURCHASE_PRICE_VARIANCE";
    public const string OperatingExpense = "OPERATING_EXPENSE";
    public const string General = "GENERAL";

    public static readonly IReadOnlyList<string> All =
    [
        HeaderGroup,
        CashBank,
        BankOutboundClearing,
        AccountsReceivable,
        InventoryAsset,
        TaxReceivable,
        GrniAccrual,
        AccountsPayable,
        TaxPayable,
        WithholdingTaxPayable,
        EquityCapital,
        SalesRevenue,
        CogsExpense,
        PurchasePriceVariance,
        OperatingExpense,
        General
    ];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? General : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported GL account category '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class FinancialPeriodStatuses
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string Locked = "LOCKED";

    public static readonly IReadOnlyList<string> All = [Open, Closed, Locked];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Open : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported financial period status '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class GlMappingPurposes
{
    public const string InventoryAsset = "INVENTORY_ASSET";
    public const string GrniClearing = "GRNI_CLEARING";
    public const string ApTradeControl = "AP_TRADE_CONTROL";
    public const string ArTradeControl = "AR_TRADE_CONTROL";
    public const string PurchasePriceVariance = "PURCHASE_PRICE_VARIANCE";
    public const string PpnInputTax = "PPN_INPUT_TAX";
    public const string PpnOutputTax = "PPN_OUTPUT_TAX";
    public const string PphWithholdingPayable = "PPH_WITHHOLDING_PAYABLE";
    public const string BankOutboundClearing = "BANK_OUTBOUND_CLEARING";
    public const string BankMainOperating = "BANK_MAIN_OPERATING";
    public const string SalesRevenueDefault = "SALES_REVENUE_DEFAULT";
    public const string CogsDefault = "COGS_DEFAULT";

    private static readonly Dictionary<string, IReadOnlyList<string>> AllowedTypes = new(StringComparer.Ordinal)
    {
        [InventoryAsset] = [GlAccountTypes.Asset],
        [GrniClearing] = [GlAccountTypes.Liability],
        [ApTradeControl] = [GlAccountTypes.Liability],
        [ArTradeControl] = [GlAccountTypes.Asset],
        [PurchasePriceVariance] = [GlAccountTypes.Expense],
        [PpnInputTax] = [GlAccountTypes.Asset],
        [PpnOutputTax] = [GlAccountTypes.Liability],
        [PphWithholdingPayable] = [GlAccountTypes.Liability],
        [BankOutboundClearing] = [GlAccountTypes.Asset, GlAccountTypes.Liability],
        [BankMainOperating] = [GlAccountTypes.Asset],
        [SalesRevenueDefault] = [GlAccountTypes.Revenue],
        [CogsDefault] = [GlAccountTypes.Expense],
    };

    public static readonly IReadOnlyList<string> All = AllowedTypes.Keys.ToList();

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!AllowedTypes.ContainsKey(value))
        {
            throw new ArgumentException($"Unsupported GL mapping purpose '{raw}'.", nameof(raw));
        }

        return value;
    }

    public static IReadOnlyList<string> GetAllowedAccountTypes(string purposeCode) =>
        AllowedTypes[Normalize(purposeCode)];
}

public static class JournalEntryKinds
{
    public const string Standard = "STANDARD";
    public const string Reversal = "REVERSAL";

    public static readonly IReadOnlyList<string> All = [Standard, Reversal];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Standard : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported journal entry kind '{raw}'.", nameof(raw));
        }

        return value;
    }
}

/// <summary>
/// Legal-entity Chart of Accounts entry governing balance classification and postability.
/// </summary>
public sealed class GlAccount
{
    public Guid AccountId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string AccountCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string AccountType { get; private set; } = GlAccountTypes.Asset;
    public string AccountCategory { get; private set; } = GlAccountCategories.General;
    public string NormalBalance { get; private set; } = GlNormalBalances.Debit;
    public Guid? ParentAccountId { get; private set; }
    public bool AllowPosting { get; private set; } = true;
    public bool IsControlAccount { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private GlAccount()
    {
    }

    public static GlAccount Create(
        Guid accountId,
        Guid tenantId,
        Guid legalEntityId,
        string accountCode,
        string name,
        string accountType,
        string accountCategory,
        Guid? parentAccountId,
        bool allowPosting,
        bool isControlAccount,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(accountId, nameof(accountId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        if (parentAccountId == accountId)
        {
            throw new ArgumentException("ParentAccountId cannot reference the account itself.", nameof(parentAccountId));
        }

        var normalizedType = GlAccountTypes.Normalize(accountType);
        var normalizedCategory = GlAccountCategories.Normalize(accountCategory);
        var effectiveAllowPosting = normalizedCategory != GlAccountCategories.HeaderGroup && allowPosting;

        return new GlAccount
        {
            AccountId = accountId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            AccountCode = GuardValidation.RequireMasterCode(accountCode, nameof(accountCode), 2, 32),
            Name = GuardValidation.RequireName(name, nameof(name), 200),
            AccountType = normalizedType,
            AccountCategory = normalizedCategory,
            NormalBalance = GlAccountTypes.DeriveNormalBalance(normalizedType),
            ParentAccountId = parentAccountId,
            AllowPosting = effectiveAllowPosting,
            IsControlAccount = isControlAccount,
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDetails(
        string name,
        string accountCategory,
        Guid? parentAccountId,
        bool allowPosting,
        bool isControlAccount,
        DateTimeOffset nowUtc)
    {
        if (parentAccountId == AccountId)
        {
            throw new ArgumentException("ParentAccountId cannot reference the account itself.", nameof(parentAccountId));
        }

        var normalizedCategory = GlAccountCategories.Normalize(accountCategory);
        Name = GuardValidation.RequireName(name, nameof(name), 200);
        AccountCategory = normalizedCategory;
        ParentAccountId = parentAccountId;
        AllowPosting = normalizedCategory != GlAccountCategories.HeaderGroup && allowPosting;
        IsControlAccount = isControlAccount;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Deactivate(DateTimeOffset nowUtc)
    {
        if (Status == MasterStatuses.Inactive)
        {
            return;
        }

        Status = MasterStatuses.Inactive;
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Activate(DateTimeOffset nowUtc)
    {
        if (Status == MasterStatuses.Active)
        {
            return;
        }

        Status = MasterStatuses.Active;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

/// <summary>
/// Legal-entity accounting period gating posting dates (`OPEN` only accepts new postings).
/// </summary>
public sealed class FinancialPeriod
{
    public Guid PeriodId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string PeriodCode { get; private set; } = string.Empty;
    public int FiscalYear { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string Status { get; private set; } = FinancialPeriodStatuses.Open;
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public string? CloseNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private FinancialPeriod()
    {
    }

    public static FinancialPeriod Create(
        Guid periodId,
        Guid tenantId,
        Guid legalEntityId,
        string periodCode,
        int fiscalYear,
        int periodNumber,
        DateOnly startDate,
        DateOnly endDate,
        string status,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(periodId, nameof(periodId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        if (fiscalYear is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "FiscalYear must be between 2000 and 2100.");
        }

        if (periodNumber is < 1 or > 13)
        {
            throw new ArgumentOutOfRangeException(nameof(periodNumber), "PeriodNumber must be between 1 and 13.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("EndDate cannot precede StartDate.", nameof(endDate));
        }

        var normalizedStatus = FinancialPeriodStatuses.Normalize(status);
        return new FinancialPeriod
        {
            PeriodId = periodId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PeriodCode = GuardValidation.RequireMasterCode(periodCode, nameof(periodCode), 2, 24),
            FiscalYear = fiscalYear,
            PeriodNumber = periodNumber,
            StartDate = startDate,
            EndDate = endDate,
            Status = normalizedStatus,
            ClosedAtUtc = normalizedStatus == FinancialPeriodStatuses.Open ? null : nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public bool ContainsDate(DateOnly date) => date >= StartDate && date <= EndDate;

    public bool IsOpenForPosting(DateOnly date) => Status == FinancialPeriodStatuses.Open && ContainsDate(date);

    public void Close(Guid actorUserId, string? note, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status != FinancialPeriodStatuses.Open)
        {
            throw new InvalidOperationException($"period_not_open: Financial period '{PeriodCode}' is already '{Status}'.");
        }

        Status = FinancialPeriodStatuses.Closed;
        ClosedAtUtc = nowUtc;
        ClosedByUserId = actorUserId;
        CloseNote = GuardValidation.NormalizeOptional(note, 300, nameof(note));
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Lock(Guid actorUserId, string? note, DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (Status == FinancialPeriodStatuses.Locked)
        {
            return;
        }

        Status = FinancialPeriodStatuses.Locked;
        ClosedAtUtc ??= nowUtc;
        ClosedByUserId ??= actorUserId;
        CloseNote = GuardValidation.NormalizeOptional(note, 300, nameof(note)) ?? CloseNote;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public static class AccountMappingValidator
{
    public static void ValidateAccountForPurpose(string purposeCode, GlAccount account, Guid expectedLegalEntityId)
    {
        ArgumentNullException.ThrowIfNull(account);
        GuardValidation.RequireNonEmptyGuid(expectedLegalEntityId, nameof(expectedLegalEntityId));

        var normalizedPurpose = GlMappingPurposes.Normalize(purposeCode);
        if (account.LegalEntityId != expectedLegalEntityId)
        {
            throw new InvalidOperationException("foreign_or_missing_account: Mapping account belongs to a different legal entity.");
        }

        if (account.Status != MasterStatuses.Active)
        {
            throw new InvalidOperationException($"account_inactive: GL account '{account.AccountCode}' is inactive.");
        }

        if (!account.AllowPosting)
        {
            throw new InvalidOperationException($"account_not_postable: GL account '{account.AccountCode}' does not allow direct postings.");
        }

        var allowed = GlMappingPurposes.GetAllowedAccountTypes(normalizedPurpose);
        if (!allowed.Contains(account.AccountType, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"mapping_account_type_mismatch: Purpose '{normalizedPurpose}' requires account type [{string.Join("/", allowed)}], got '{account.AccountType}'.");
        }
    }
}

/// <summary>
/// Maps canonical subledger accounting purposes (Inventory, GRNI, AP, AR, Tax, Bank) to entity posting accounts.
/// </summary>
public sealed class AccountMapping
{
    public Guid MappingId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string PurposeCode { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public string? Notes { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private AccountMapping()
    {
    }

    public static AccountMapping Create(
        Guid mappingId,
        Guid tenantId,
        Guid legalEntityId,
        string purposeCode,
        GlAccount account,
        string? notes,
        Guid actorUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(mappingId, nameof(mappingId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(account);

        if (account.TenantId != tenantId)
        {
            throw new InvalidOperationException("foreign_or_missing_account: Account tenant mismatch.");
        }

        var normalizedPurpose = GlMappingPurposes.Normalize(purposeCode);
        AccountMappingValidator.ValidateAccountForPurpose(normalizedPurpose, account, legalEntityId);

        return new AccountMapping
        {
            MappingId = mappingId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PurposeCode = normalizedPurpose,
            AccountId = account.AccountId,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            UpdatedByUserId = actorUserId,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateAccount(GlAccount account, string? notes, Guid actorUserId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(account);
        GuardValidation.RequireNonEmptyGuid(actorUserId, nameof(actorUserId));
        if (account.TenantId != TenantId)
        {
            throw new InvalidOperationException("foreign_or_missing_account: Account tenant mismatch.");
        }

        AccountMappingValidator.ValidateAccountForPurpose(PurposeCode, account, LegalEntityId);
        AccountId = account.AccountId;
        Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes));
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public sealed class JournalSequence
{
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PeriodId { get; private set; }
    public long LastNumber { get; private set; }

    private JournalSequence()
    {
    }
}

public sealed record GlJournalLineInput(
    Guid AccountId,
    string? Description,
    decimal DebitAmount,
    decimal CreditAmount,
    Guid? PartnerId = null,
    Guid? ItemId = null,
    Guid? TaxCodeId = null);

public static class GlPostingHash
{
    public static string ComputePayloadHash(
        DateOnly postingDate,
        string currencyCode,
        string entryKind,
        Guid? reversalOfJournalId,
        IReadOnlyList<GlJournalLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var normalizedCurrency = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode));
        var normalizedKind = JournalEntryKinds.Normalize(entryKind);

        var sb = new StringBuilder();
        sb.Append(postingDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            .Append('|')
            .Append(normalizedCurrency)
            .Append('|')
            .Append(normalizedKind)
            .Append('|')
            .Append(reversalOfJournalId?.ToString("D") ?? string.Empty);

        foreach (var line in lines)
        {
            sb.Append('\n')
                .Append(line.AccountId.ToString("D"))
                .Append('|')
                .Append(line.DebitAmount.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
                .Append('|')
                .Append(line.CreditAmount.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))
                .Append('|')
                .Append(line.PartnerId?.ToString("D") ?? string.Empty)
                .Append('|')
                .Append(line.ItemId?.ToString("D") ?? string.Empty)
                .Append('|')
                .Append(line.TaxCodeId?.ToString("D") ?? string.Empty);
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(hashBytes);
    }
}

/// <summary>
/// Immutable append-only journal header linked to one entity, fiscal period, and idempotent source key.
/// </summary>
public sealed class JournalEntry
{
    public Guid JournalId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PeriodId { get; private set; }
    public string JournalNumber { get; private set; } = string.Empty;
    public string EntryKind { get; private set; } = JournalEntryKinds.Standard;
    public DateOnly PostingDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public string Description { get; private set; } = string.Empty;
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceDocumentType { get; private set; } = string.Empty;
    public string SourceDocumentId { get; private set; } = string.Empty;
    public string SourceEventKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public Guid? ReversalOfJournalId { get; private set; }
    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public int LineCount { get; private set; }
    public Guid PostedByUserId { get; private set; }
    public DateTimeOffset PostedAtUtc { get; private set; }

    private JournalEntry()
    {
    }

    public static (JournalEntry Entry, List<JournalLine> Lines) CreatePosted(
        Guid journalId,
        Guid tenantId,
        Guid legalEntityId,
        Guid periodId,
        string journalNumber,
        string entryKind,
        DateOnly postingDate,
        string currencyCode,
        string description,
        string sourceModule,
        string sourceDocumentType,
        string sourceDocumentId,
        string sourceEventKey,
        Guid? reversalOfJournalId,
        IReadOnlyList<GlJournalLineInput> lineInputs,
        Guid postedByUserId,
        DateTimeOffset postedAtUtc)
    {
        GuardValidation.RequireNonEmptyGuid(journalId, nameof(journalId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(periodId, nameof(periodId));
        GuardValidation.RequireNonEmptyGuid(postedByUserId, nameof(postedByUserId));

        ArgumentNullException.ThrowIfNull(lineInputs);
        if (lineInputs.Count < 2)
        {
            throw new InvalidOperationException("invalid_journal_lines: Journal entry must contain at least 2 balanced lines.");
        }

        var normalizedKind = JournalEntryKinds.Normalize(entryKind);
        if (normalizedKind == JournalEntryKinds.Reversal && (reversalOfJournalId is null || reversalOfJournalId == Guid.Empty))
        {
            throw new ArgumentException("Reversal journal entries must link to ReversalOfJournalId.", nameof(reversalOfJournalId));
        }

        if (normalizedKind == JournalEntryKinds.Standard && reversalOfJournalId is not null)
        {
            throw new ArgumentException("Standard journal entries cannot specify ReversalOfJournalId.", nameof(reversalOfJournalId));
        }

        var normalizedCurrency = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode));
        var lines = new List<JournalLine>(lineInputs.Count);
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        for (var i = 0; i < lineInputs.Count; i++)
        {
            var input = lineInputs[i];
            GuardValidation.RequireNonEmptyGuid(input.AccountId, nameof(input.AccountId));

            var debit = input.DebitAmount;
            var credit = input.CreditAmount;
            if (debit < 0m || credit < 0m)
            {
                throw new ArgumentException("Journal line debit and credit amounts must be non-negative.", nameof(lineInputs));
            }

            if (decimal.Round(debit, 4, MidpointRounding.AwayFromZero) != debit ||
                decimal.Round(credit, 4, MidpointRounding.AwayFromZero) != credit)
            {
                throw new ArgumentException("Journal line amounts support at most 4 decimal places.", nameof(lineInputs));
            }

            var isSingleSidedPositive = (debit > 0m && credit == 0m) || (credit > 0m && debit == 0m);
            if (!isSingleSidedPositive)
            {
                throw new ArgumentException(
                    $"Journal line #{i + 1} must have strictly positive Debit or Credit (never both, never zero).",
                    nameof(lineInputs));
            }

            totalDebit += debit;
            totalCredit += credit;
            lines.Add(JournalLine.Create(
                Guid.NewGuid(),
                tenantId,
                legalEntityId,
                journalId,
                i + 1,
                input.AccountId,
                input.Description,
                debit,
                credit,
                input.PartnerId,
                input.ItemId,
                input.TaxCodeId));
        }

        if (totalDebit <= 0m || totalDebit != totalCredit)
        {
            throw new InvalidOperationException(
                $"unbalanced_journal: Journal entry is unbalanced (TotalDebit={totalDebit:F4}, TotalCredit={totalCredit:F4}).");
        }

        var entry = new JournalEntry
        {
            JournalId = journalId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PeriodId = periodId,
            JournalNumber = GuardValidation.RequireMasterCode(journalNumber, nameof(journalNumber), 2, 40),
            EntryKind = normalizedKind,
            PostingDate = postingDate,
            CurrencyCode = normalizedCurrency,
            Description = GuardValidation.RequireName(description, nameof(description), 300),
            SourceModule = GuardValidation.RequireMasterCode(sourceModule, nameof(sourceModule), 2, 40),
            SourceDocumentType = GuardValidation.RequireMasterCode(sourceDocumentType, nameof(sourceDocumentType), 2, 48),
            SourceDocumentId = GuardValidation.RequireName(sourceDocumentId, nameof(sourceDocumentId), 100),
            SourceEventKey = GuardValidation.RequireName(sourceEventKey, nameof(sourceEventKey), 160),
            PayloadHash = GlPostingHash.ComputePayloadHash(postingDate, normalizedCurrency, normalizedKind, reversalOfJournalId, lineInputs),
            ReversalOfJournalId = reversalOfJournalId,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            LineCount = lines.Count,
            PostedByUserId = postedByUserId,
            PostedAtUtc = postedAtUtc
        };

        return (entry, lines);
    }
}

/// <summary>
/// Immutable append-only journal line belonging to a single entity and posted journal header.
/// </summary>
public sealed class JournalLine
{
    public Guid LineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid JournalId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid AccountId { get; private set; }
    public string? Description { get; private set; }
    public decimal DebitAmount { get; private set; }
    public decimal CreditAmount { get; private set; }
    public Guid? PartnerId { get; private set; }
    public Guid? ItemId { get; private set; }
    public Guid? TaxCodeId { get; private set; }

    private JournalLine()
    {
    }

    internal static JournalLine Create(
        Guid lineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid journalId,
        int lineNumber,
        Guid accountId,
        string? description,
        decimal debitAmount,
        decimal creditAmount,
        Guid? partnerId,
        Guid? itemId,
        Guid? taxCodeId) =>
        new()
        {
            LineId = lineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            JournalId = journalId,
            LineNumber = lineNumber,
            AccountId = accountId,
            Description = GuardValidation.NormalizeOptional(description, 240, nameof(description)),
            DebitAmount = debitAmount,
            CreditAmount = creditAmount,
            PartnerId = partnerId,
            ItemId = itemId,
            TaxCodeId = taxCodeId
        };
}
