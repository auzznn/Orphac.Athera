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

public static class ReceiptAccrualStatuses
{
    public const string Open = "OPEN";
    public const string PartiallyCleared = "PARTIALLY_CLEARED";
    public const string Cleared = "CLEARED";

    public static readonly IReadOnlyList<string> All = [Open, PartiallyCleared, Cleared];
}

/// <summary>
/// GRNI subledger header created atomically when Gate 2 valued acceptance posts Dr Inventory Asset / Cr GRNI.
/// </summary>
public sealed class ReceiptAccrual
{
    public Guid ReceiptAccrualId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string AccrualNumber { get; private set; } = string.Empty;
    public Guid ValuedAcceptanceId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierPartnerId { get; private set; }
    public Guid JournalId { get; private set; }
    public Guid InventoryAssetAccountId { get; private set; }
    public Guid GrniAccountId { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public string CurrencyCode { get; private set; } = "IDR";
    public decimal AccruedAmount { get; private set; }
    public decimal ClearedAmount { get; private set; }
    public decimal RemainingOpenAmount { get; private set; }
    public string Status { get; private set; } = ReceiptAccrualStatuses.Open;
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ReceiptAccrual()
    {
    }

    public static ReceiptAccrual Create(
        Guid receiptAccrualId,
        Guid tenantId,
        Guid legalEntityId,
        string accrualNumber,
        Guid valuedAcceptanceId,
        Guid goodsReceiptId,
        Guid purchaseOrderId,
        Guid supplierPartnerId,
        Guid journalId,
        Guid inventoryAssetAccountId,
        Guid grniAccountId,
        DateOnly postingDate,
        string currencyCode,
        decimal accruedAmount,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(receiptAccrualId, nameof(receiptAccrualId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceId, nameof(valuedAcceptanceId));
        GuardValidation.RequireNonEmptyGuid(goodsReceiptId, nameof(goodsReceiptId));
        GuardValidation.RequireNonEmptyGuid(purchaseOrderId, nameof(purchaseOrderId));
        GuardValidation.RequireNonEmptyGuid(supplierPartnerId, nameof(supplierPartnerId));
        GuardValidation.RequireNonEmptyGuid(journalId, nameof(journalId));
        GuardValidation.RequireNonEmptyGuid(inventoryAssetAccountId, nameof(inventoryAssetAccountId));
        GuardValidation.RequireNonEmptyGuid(grniAccountId, nameof(grniAccountId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var amount = decimal.Round(accruedAmount, 4, MidpointRounding.AwayFromZero);
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(accruedAmount), "Accrued GRNI amount must be positive.");
        }

        return new ReceiptAccrual
        {
            ReceiptAccrualId = receiptAccrualId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            AccrualNumber = GuardValidation.RequireMasterCode(accrualNumber, nameof(accrualNumber), 2, 40),
            ValuedAcceptanceId = valuedAcceptanceId,
            GoodsReceiptId = goodsReceiptId,
            PurchaseOrderId = purchaseOrderId,
            SupplierPartnerId = supplierPartnerId,
            JournalId = journalId,
            InventoryAssetAccountId = inventoryAssetAccountId,
            GrniAccountId = grniAccountId,
            PostingDate = postingDate,
            CurrencyCode = GuardValidation.RequireExactIsoCode(currencyCode, "IDR", 3, nameof(currencyCode)),
            AccruedAmount = amount,
            ClearedAmount = 0m,
            RemainingOpenAmount = amount,
            Status = ReceiptAccrualStatuses.Open,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void ApplyLineClearings(IReadOnlyList<ReceiptAccrualLine> lines, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var sumCleared = decimal.Round(lines.Sum(l => l.ClearedLineAmount), 4, MidpointRounding.AwayFromZero);
        var sumRemaining = decimal.Round(lines.Sum(l => l.RemainingOpenLineAmount), 4, MidpointRounding.AwayFromZero);

        if (sumCleared < 0m || sumRemaining < 0m || sumCleared + sumRemaining != AccruedAmount)
        {
            throw new InvalidOperationException(
                $"grni_accrual_clearing_conservation_failed: Accrual '{AccrualNumber}' cleared ({sumCleared:F4}) + open ({sumRemaining:F4}) does not equal accrued ({AccruedAmount:F4}).");
        }

        ClearedAmount = sumCleared;
        RemainingOpenAmount = sumRemaining;
        Status = RemainingOpenAmount == 0m
            ? ReceiptAccrualStatuses.Cleared
            : ClearedAmount > 0m
                ? ReceiptAccrualStatuses.PartiallyCleared
                : ReceiptAccrualStatuses.Open;
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

/// <summary>
/// GRNI subledger line linking an accepted receipt line to open unbilled PO capacity for S09 three-way matching.
/// </summary>
public sealed class ReceiptAccrualLine
{
    public Guid ReceiptAccrualLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ReceiptAccrualId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ValuedAcceptanceLineId { get; private set; }
    public Guid GoodsReceiptLineId { get; private set; }
    public Guid PoLineId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal AcceptedBaseQuantity { get; private set; }
    public decimal BilledBaseQuantity { get; private set; }
    public decimal RemainingUnbilledBaseQuantity { get; private set; }
    public decimal UnitCostBase { get; private set; }
    public decimal AccruedLineAmount { get; private set; }
    public decimal ClearedLineAmount { get; private set; }
    public decimal RemainingOpenLineAmount { get; private set; }

    private ReceiptAccrualLine()
    {
    }

    public static ReceiptAccrualLine Create(
        Guid receiptAccrualLineId,
        Guid tenantId,
        Guid legalEntityId,
        Guid receiptAccrualId,
        int lineNumber,
        Guid valuedAcceptanceLineId,
        Guid goodsReceiptLineId,
        Guid poLineId,
        Guid itemId,
        decimal acceptedBaseQuantity,
        decimal unitCostBase,
        decimal accruedLineAmount)
    {
        GuardValidation.RequireNonEmptyGuid(receiptAccrualLineId, nameof(receiptAccrualLineId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualId, nameof(receiptAccrualId));
        GuardValidation.RequireNonEmptyGuid(valuedAcceptanceLineId, nameof(valuedAcceptanceLineId));
        GuardValidation.RequireNonEmptyGuid(goodsReceiptLineId, nameof(goodsReceiptLineId));
        GuardValidation.RequireNonEmptyGuid(poLineId, nameof(poLineId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));

        if (lineNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "LineNumber must be >= 1.");
        }

        var qty = decimal.Round(acceptedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var unitCost = decimal.Round(unitCostBase, 6, MidpointRounding.AwayFromZero);
        var amount = decimal.Round(accruedLineAmount, 4, MidpointRounding.AwayFromZero);
        if (qty <= 0m || unitCost <= 0m || amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptedBaseQuantity), "Accrual line requires positive quantity, unit cost and amount.");
        }

        return new ReceiptAccrualLine
        {
            ReceiptAccrualLineId = receiptAccrualLineId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ReceiptAccrualId = receiptAccrualId,
            LineNumber = lineNumber,
            ValuedAcceptanceLineId = valuedAcceptanceLineId,
            GoodsReceiptLineId = goodsReceiptLineId,
            PoLineId = poLineId,
            ItemId = itemId,
            AcceptedBaseQuantity = qty,
            BilledBaseQuantity = 0m,
            RemainingUnbilledBaseQuantity = qty,
            UnitCostBase = unitCost,
            AccruedLineAmount = amount,
            ClearedLineAmount = 0m,
            RemainingOpenLineAmount = amount
        };
    }

    public decimal CalculateClearAmount(decimal billedBaseQuantity)
    {
        var qty = decimal.Round(billedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(billedBaseQuantity), "Billed quantity must be positive.");
        }

        if (qty > RemainingUnbilledBaseQuantity)
        {
            throw new InvalidOperationException(
                $"dq_exceeds_unbilled_accepted_capacity: Billed quantity {qty:0.######} exceeds remaining unbilled receipt capacity {RemainingUnbilledBaseQuantity:0.######}.");
        }

        if (qty == RemainingUnbilledBaseQuantity)
        {
            return RemainingOpenLineAmount;
        }

        var proportional = decimal.Round(qty * UnitCostBase, 4, MidpointRounding.AwayFromZero);
        if (proportional <= 0m)
        {
            proportional = 0.0001m;
        }

        if (proportional >= RemainingOpenLineAmount)
        {
            proportional = decimal.Round(RemainingOpenLineAmount - 0.0001m, 4, MidpointRounding.AwayFromZero);
        }

        return proportional;
    }

    public decimal ApplyClearing(decimal billedBaseQuantity, decimal? explicitClearAmount = null)
    {
        var qty = decimal.Round(billedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var clearAmt = explicitClearAmount.HasValue
            ? decimal.Round(explicitClearAmount.Value, 4, MidpointRounding.AwayFromZero)
            : CalculateClearAmount(qty);

        if (qty <= 0m || qty > RemainingUnbilledBaseQuantity)
        {
            throw new InvalidOperationException(
                $"dq_exceeds_unbilled_accepted_capacity: Billed quantity {qty:0.######} exceeds remaining unbilled receipt capacity {RemainingUnbilledBaseQuantity:0.######}.");
        }

        if (clearAmt <= 0m || clearAmt > RemainingOpenLineAmount)
        {
            throw new InvalidOperationException(
                $"grni_clearing_amount_invalid: Clear amount {clearAmt:F4} is invalid for open GRNI line amount {RemainingOpenLineAmount:F4}.");
        }

        BilledBaseQuantity = decimal.Round(BilledBaseQuantity + qty, 6, MidpointRounding.AwayFromZero);
        RemainingUnbilledBaseQuantity = decimal.Round(AcceptedBaseQuantity - BilledBaseQuantity, 6, MidpointRounding.AwayFromZero);
        ClearedLineAmount = decimal.Round(ClearedLineAmount + clearAmt, 4, MidpointRounding.AwayFromZero);
        RemainingOpenLineAmount = decimal.Round(AccruedLineAmount - ClearedLineAmount, 4, MidpointRounding.AwayFromZero);

        if ((RemainingUnbilledBaseQuantity == 0m && RemainingOpenLineAmount != 0m) ||
            (RemainingUnbilledBaseQuantity > 0m && RemainingOpenLineAmount <= 0m))
        {
            throw new InvalidOperationException(
                $"grni_line_conservation_failed: Partial GRNI clearing left mismatched qty={RemainingUnbilledBaseQuantity:0.######} vs open={RemainingOpenLineAmount:F4}.");
        }

        return clearAmt;
    }
}

/// <summary>
/// Strictly append-only GRNI clearing evidence linking S07 accrual capacity to S09 posted supplier bills.
/// </summary>
public sealed class ReceiptAccrualClearing
{
    public Guid ClearingId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ReceiptAccrualId { get; private set; }
    public Guid ReceiptAccrualLineId { get; private set; }
    public Guid SupplierBillId { get; private set; }
    public Guid SupplierBillLineId { get; private set; }
    public Guid GlJournalId { get; private set; }
    public decimal ClearedBaseQuantity { get; private set; }
    public decimal ClearedAmount { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ReceiptAccrualClearing()
    {
    }

    public static ReceiptAccrualClearing Create(
        Guid clearingId,
        Guid tenantId,
        Guid legalEntityId,
        Guid receiptAccrualId,
        Guid receiptAccrualLineId,
        Guid supplierBillId,
        Guid supplierBillLineId,
        Guid glJournalId,
        decimal clearedBaseQuantity,
        decimal clearedAmount,
        DateOnly postingDate,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(clearingId, nameof(clearingId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualId, nameof(receiptAccrualId));
        GuardValidation.RequireNonEmptyGuid(receiptAccrualLineId, nameof(receiptAccrualLineId));
        GuardValidation.RequireNonEmptyGuid(supplierBillId, nameof(supplierBillId));
        GuardValidation.RequireNonEmptyGuid(supplierBillLineId, nameof(supplierBillLineId));
        GuardValidation.RequireNonEmptyGuid(glJournalId, nameof(glJournalId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));

        var qty = decimal.Round(clearedBaseQuantity, 6, MidpointRounding.AwayFromZero);
        var amt = decimal.Round(clearedAmount, 4, MidpointRounding.AwayFromZero);
        if (qty <= 0m || amt <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(clearedBaseQuantity), "Cleared GRNI quantity and amount must be positive.");
        }

        return new ReceiptAccrualClearing
        {
            ClearingId = clearingId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ReceiptAccrualId = receiptAccrualId,
            ReceiptAccrualLineId = receiptAccrualLineId,
            SupplierBillId = supplierBillId,
            SupplierBillLineId = supplierBillLineId,
            GlJournalId = glJournalId,
            ClearedBaseQuantity = qty,
            ClearedAmount = amt,
            PostingDate = postingDate,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}
