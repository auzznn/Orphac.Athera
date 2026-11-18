using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Tax;

public static class TaxFamilies
{
    public const string Ppn = "PPN";
    public const string Pph = "PPH";

    public static readonly IReadOnlyList<string> All = [Ppn, Pph];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported tax family '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class TaxDirections
{
    public const string Input = "INPUT";
    public const string Output = "OUTPUT";
    public const string Withholding = "WITHHOLDING";

    public static readonly IReadOnlyList<string> All = [Input, Output, Withholding];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported tax direction '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class TaxTreatmentClasses
{
    public const string StandardTaxable = "STANDARD_TAXABLE";
    public const string SpecialDpp1112 = "SPECIAL_DPP_11_12";
    public const string ZeroRated = "ZERO_RATED";
    public const string Exempt = "EXEMPT";
    public const string NotCollectedFacility = "NOT_COLLECTED_FACILITY";
    public const string WithholdingDeduction = "WITHHOLDING_DEDUCTION";

    public static readonly IReadOnlyList<string> All =
    [
        StandardTaxable,
        SpecialDpp1112,
        ZeroRated,
        Exempt,
        NotCollectedFacility,
        WithholdingDeduction
    ];

    public static string Normalize(string? raw)
    {
        var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported tax treatment class '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class TaxReviewStatuses
{
    public const string UnreviewedConfigurable = "UNREVIEWED_CONFIGURABLE";
    public const string ReviewedInternal = "REVIEWED_INTERNAL";

    public static readonly IReadOnlyList<string> All = [UnreviewedConfigurable, ReviewedInternal];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? UnreviewedConfigurable : raw.Trim().ToUpperInvariant();
        if (!All.Contains(value, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported tax review status '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class TaxEvidenceStatuses
{
    public const string NotRequired = "NOT_REQUIRED";
    public const string EvidenceProvided = "EVIDENCE_PROVIDED";
    public const string EvidencePendingReview = "EVIDENCE_PENDING_REVIEW";
}

/// <summary>
/// Effective-dated configurable Indonesian tax rule scoped to a legal entity (`OD-07`).
/// Never implies automatic statutory filing compliance until signed by the Product Owner / Tax Advisor.
/// </summary>
public sealed class TaxCode
{
    public Guid TaxCodeId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TaxFamily { get; private set; } = TaxFamilies.Ppn;
    public string Direction { get; private set; } = TaxDirections.Input;
    public string TreatmentClass { get; private set; } = TaxTreatmentClasses.StandardTaxable;
    public decimal NominalRatePercent { get; private set; }
    public int TaxableBaseRatioNumerator { get; private set; } = 1;
    public int TaxableBaseRatioDenominator { get; private set; } = 1;
    public bool RequiresDocumentaryEvidence { get; private set; }
    public bool RequiresCounterpartyNpwp { get; private set; }
    public Guid? TaxAccountId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string ReviewStatus { get; private set; } = TaxReviewStatuses.UnreviewedConfigurable;
    public string? StatutoryReferenceNote { get; private set; }
    public string Status { get; private set; } = MasterStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private TaxCode()
    {
    }

    public static TaxCode Create(
        Guid taxCodeId,
        Guid tenantId,
        Guid legalEntityId,
        string code,
        string name,
        string taxFamily,
        string direction,
        string treatmentClass,
        decimal nominalRatePercent,
        int taxableBaseRatioNumerator,
        int taxableBaseRatioDenominator,
        bool requiresDocumentaryEvidence,
        bool requiresCounterpartyNpwp,
        Guid? taxAccountId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string reviewStatus,
        string? statutoryReferenceNote,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(taxCodeId, nameof(taxCodeId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));

        if (nominalRatePercent < 0m || nominalRatePercent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalRatePercent), "NominalRatePercent must be between 0 and 100.");
        }

        if (taxableBaseRatioNumerator < 1 || taxableBaseRatioDenominator < taxableBaseRatioNumerator)
        {
            throw new ArgumentException("TaxableBaseRatioNumerator must be >= 1 and <= Denominator.", nameof(taxableBaseRatioNumerator));
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("EffectiveTo cannot precede EffectiveFrom.", nameof(effectiveTo));
        }

        return new TaxCode
        {
            TaxCodeId = taxCodeId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = GuardValidation.RequireMasterCode(code, nameof(code), 2, 32),
            Name = GuardValidation.RequireName(name, nameof(name), 200),
            TaxFamily = TaxFamilies.Normalize(taxFamily),
            Direction = TaxDirections.Normalize(direction),
            TreatmentClass = TaxTreatmentClasses.Normalize(treatmentClass),
            NominalRatePercent = decimal.Round(nominalRatePercent, 4, MidpointRounding.AwayFromZero),
            TaxableBaseRatioNumerator = taxableBaseRatioNumerator,
            TaxableBaseRatioDenominator = taxableBaseRatioDenominator,
            RequiresDocumentaryEvidence = requiresDocumentaryEvidence,
            RequiresCounterpartyNpwp = requiresCounterpartyNpwp,
            TaxAccountId = taxAccountId,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            ReviewStatus = TaxReviewStatuses.Normalize(reviewStatus),
            StatutoryReferenceNote = GuardValidation.NormalizeOptional(statutoryReferenceNote, 300, nameof(statutoryReferenceNote)),
            Status = MasterStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public bool IsEffectiveOn(DateOnly date) =>
        Status == MasterStatuses.Active && date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo.Value);
}

public sealed record TaxDeterminationEnvelope(
    Guid TaxCodeId,
    string Code,
    string TaxFamily,
    string Direction,
    string TreatmentClass,
    decimal GrossAmount,
    decimal TaxableBaseAmount,
    decimal NominalRatePercent,
    decimal EffectiveRatePercent,
    decimal TaxAmount,
    string ReviewStatus,
    string EvidenceStatus,
    bool IsBlocked,
    string? BlockedReason,
    string GovernanceDisclaimer);

/// <summary>
/// Evaluates explicit effective-dated tax rules using decimal math (`OD-07` explicit selection only).
/// </summary>
public static class TaxDeterminationEvaluator
{
    public const string Od07Disclaimer =
        "OD-07 Configurable Rule: Statutory Indonesian tax calculation & filing require Product Owner / Tax Advisor sign-off (ATH-CR-002).";

    public static TaxDeterminationEnvelope Evaluate(
        TaxCode? taxCode,
        decimal grossAmount,
        DateOnly transactionDate,
        bool hasCounterpartyNpwp,
        bool evidenceProvided,
        int currencyScale = 0)
    {
        if (taxCode is null)
        {
            throw new InvalidOperationException(
                "explicit_tax_code_required: Select an explicit effective-dated tax code; tax is never inferred from partner PKP/NPWP status alone.");
        }

        if (grossAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(grossAmount), "GrossAmount must be positive.");
        }

        if (currencyScale is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(currencyScale), "CurrencyScale must be between 0 and 4.");
        }

        if (!taxCode.IsEffectiveOn(transactionDate))
        {
            throw new InvalidOperationException(
                $"tax_code_not_effective_on_date: Tax code '{taxCode.Code}' ({taxCode.Status}) is not active on {transactionDate:yyyy-MM-dd}.");
        }

        var dpp = decimal.Round(
            grossAmount * taxCode.TaxableBaseRatioNumerator / taxCode.TaxableBaseRatioDenominator,
            currencyScale,
            MidpointRounding.AwayFromZero);

        var taxAmount = decimal.Round(
            dpp * (taxCode.NominalRatePercent / 100m),
            currencyScale,
            MidpointRounding.AwayFromZero);

        var effectiveRate = decimal.Round(
            taxCode.NominalRatePercent * taxCode.TaxableBaseRatioNumerator / taxCode.TaxableBaseRatioDenominator,
            4,
            MidpointRounding.AwayFromZero);

        var evidenceStatus = !taxCode.RequiresDocumentaryEvidence
            ? TaxEvidenceStatuses.NotRequired
            : evidenceProvided
                ? TaxEvidenceStatuses.EvidenceProvided
                : TaxEvidenceStatuses.EvidencePendingReview;

        var isBlocked = taxCode.RequiresCounterpartyNpwp && !hasCounterpartyNpwp;
        var blockedReason = isBlocked ? "counterparty_npwp_required_for_rate" : null;

        return new TaxDeterminationEnvelope(
            taxCode.TaxCodeId,
            taxCode.Code,
            taxCode.TaxFamily,
            taxCode.Direction,
            taxCode.TreatmentClass,
            grossAmount,
            dpp,
            taxCode.NominalRatePercent,
            effectiveRate,
            taxAmount,
            taxCode.ReviewStatus,
            evidenceStatus,
            isBlocked,
            blockedReason,
            Od07Disclaimer);
    }
}

public static class TaxDeterminationRoles
{
    public const string PpnInput = "PPN_INPUT";
    public const string PpnInputReversal = "PPN_INPUT_REVERSAL";
    public const string PphWithholding = "PPH_WITHHOLDING";

    public static readonly IReadOnlyList<string> All = [PpnInput, PpnInputReversal, PphWithholding];
}

/// <summary>
/// Strictly append-only versioned tax calculation evidence recorded when posting transactional documents (`OD-07`).
/// </summary>
public sealed class TaxDetermination
{
    public Guid DeterminationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string SourceModule { get; private set; } = string.Empty;
    public string SourceDocumentType { get; private set; } = string.Empty;
    public Guid SourceDocumentId { get; private set; }
    public Guid SourceDocumentLineId { get; private set; }
    public string TaxRole { get; private set; } = TaxDeterminationRoles.PpnInput;
    public Guid TaxCodeId { get; private set; }
    public string TaxCodeSnapshot { get; private set; } = string.Empty;
    public string TaxFamily { get; private set; } = TaxFamilies.Ppn;
    public string Direction { get; private set; } = TaxDirections.Input;
    public string TreatmentClass { get; private set; } = TaxTreatmentClasses.StandardTaxable;
    public decimal GrossAmount { get; private set; }
    public decimal TaxableBaseAmount { get; private set; }
    public decimal NominalRatePercent { get; private set; }
    public decimal EffectiveRatePercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public string ReviewStatus { get; private set; } = TaxReviewStatuses.UnreviewedConfigurable;
    public string EvidenceStatus { get; private set; } = TaxEvidenceStatuses.NotRequired;
    public string? DocumentaryEvidenceRef { get; private set; }
    public string? CounterpartyNpwpSnapshot { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TaxDetermination()
    {
    }

    public static TaxDetermination FromEnvelope(
        Guid determinationId,
        Guid tenantId,
        Guid legalEntityId,
        string sourceModule,
        string sourceDocumentType,
        Guid sourceDocumentId,
        Guid sourceDocumentLineId,
        string taxRole,
        TaxDeterminationEnvelope envelope,
        string? documentaryEvidenceRef,
        string? counterpartyNpwpSnapshot,
        DateOnly transactionDate,
        Guid createdByUserId,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(determinationId, nameof(determinationId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(sourceDocumentId, nameof(sourceDocumentId));
        GuardValidation.RequireNonEmptyGuid(sourceDocumentLineId, nameof(sourceDocumentLineId));
        GuardValidation.RequireNonEmptyGuid(createdByUserId, nameof(createdByUserId));
        ArgumentNullException.ThrowIfNull(envelope);

        var normalizedRole = taxRole?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!TaxDeterminationRoles.All.Contains(normalizedRole, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported tax determination role '{taxRole}'.", nameof(taxRole));
        }

        if (envelope.IsBlocked)
        {
            throw new InvalidOperationException(
                envelope.BlockedReason ?? "tax_determination_blocked: Blocked tax determination cannot be posted.");
        }

        return new TaxDetermination
        {
            DeterminationId = determinationId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            SourceModule = GuardValidation.RequireMasterCode(sourceModule, nameof(sourceModule), 2, 32),
            SourceDocumentType = GuardValidation.RequireMasterCode(sourceDocumentType, nameof(sourceDocumentType), 2, 48),
            SourceDocumentId = sourceDocumentId,
            SourceDocumentLineId = sourceDocumentLineId,
            TaxRole = normalizedRole,
            TaxCodeId = envelope.TaxCodeId,
            TaxCodeSnapshot = GuardValidation.RequireMasterCode(envelope.Code, nameof(envelope.Code), 2, 32),
            TaxFamily = envelope.TaxFamily,
            Direction = envelope.Direction,
            TreatmentClass = envelope.TreatmentClass,
            GrossAmount = envelope.GrossAmount,
            TaxableBaseAmount = envelope.TaxableBaseAmount,
            NominalRatePercent = envelope.NominalRatePercent,
            EffectiveRatePercent = envelope.EffectiveRatePercent,
            TaxAmount = envelope.TaxAmount,
            ReviewStatus = envelope.ReviewStatus,
            EvidenceStatus = envelope.EvidenceStatus,
            DocumentaryEvidenceRef = GuardValidation.NormalizeOptional(documentaryEvidenceRef, 120, nameof(documentaryEvidenceRef)),
            CounterpartyNpwpSnapshot = GuardValidation.NormalizeOptional(counterpartyNpwpSnapshot, 64, nameof(counterpartyNpwpSnapshot)),
            TransactionDate = transactionDate,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc
        };
    }
}
