using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Partners;

public static class PartnerStatuses
{
    public const string Active = MasterStatuses.Active;
    public const string Inactive = MasterStatuses.Inactive;
    public static readonly IReadOnlyList<string> All = MasterStatuses.All;
}

public static class TaxPkpStatuses
{
    public const string Unknown = "UNKNOWN";
    public const string Pkp = "PKP";
    public const string NonPkp = "NON_PKP";

    public static readonly IReadOnlyList<string> All = [Unknown, Pkp, NonPkp];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Unknown : raw.Trim().ToUpperInvariant();
        if (value is not (Unknown or Pkp or NonPkp))
        {
            throw new ArgumentException($"Unsupported PKP tax metadata status '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class PartnerContactRoles
{
    public const string Commercial = "COMMERCIAL";
    public const string Billing = "BILLING";
    public const string ReceivingDispatch = "RECEIVING_DISPATCH";

    public static readonly IReadOnlyList<string> All = [Commercial, Billing, ReceivingDispatch];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? Commercial : raw.Trim().ToUpperInvariant();
        if (value is not (Commercial or Billing or ReceivingDispatch))
        {
            throw new ArgumentException($"Unsupported partner contact role '{raw}'.", nameof(raw));
        }

        return value;
    }
}

/// <summary>
/// Tenant-shared business partner capable of serving Supplier, Customer, or both roles.
/// Tax NPWP and PKP status are stored strictly as master-data metadata pending OD-07.
/// </summary>
public sealed class Partner
{
    public Guid PartnerId { get; private set; }
    public Guid TenantId { get; private set; }
    public string PartnerCode { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsSupplier { get; private set; }
    public bool IsCustomer { get; private set; }
    public string? TaxRegistrationNpwp { get; private set; }
    public string TaxPkpStatus { get; private set; } = TaxPkpStatuses.Unknown;
    public string EntityScopeMode { get; private set; } = EntityScopeModes.AllEntities;
    public string Status { get; private set; } = PartnerStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private Partner()
    {
    }

    public static Partner Create(
        Guid partnerId,
        Guid tenantId,
        string code,
        string legalName,
        string displayName,
        bool isSupplier,
        bool isCustomer,
        string? npwp,
        string taxPkpStatus,
        string entityScopeMode,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(partnerId, nameof(partnerId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        RequireAtLeastOneRole(isSupplier, isCustomer);

        return new Partner
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            PartnerCode = GuardValidation.RequireMasterCode(code, nameof(code)),
            LegalName = GuardValidation.RequireName(legalName, nameof(legalName), 200),
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? GuardValidation.RequireName(legalName, nameof(legalName), 160)
                : GuardValidation.RequireName(displayName, nameof(displayName), 160),
            IsSupplier = isSupplier,
            IsCustomer = isCustomer,
            TaxRegistrationNpwp = GuardValidation.NormalizeOptional(npwp, 64, nameof(npwp)),
            TaxPkpStatus = TaxPkpStatuses.Normalize(taxPkpStatus),
            EntityScopeMode = EntityScopeModes.Normalize(entityScopeMode),
            Status = PartnerStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDetails(
        string legalName,
        string displayName,
        bool isSupplier,
        bool isCustomer,
        string? npwp,
        string taxPkpStatus,
        string entityScopeMode,
        DateTimeOffset nowUtc)
    {
        RequireAtLeastOneRole(isSupplier, isCustomer);

        LegalName = GuardValidation.RequireName(legalName, nameof(legalName), 200);
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? GuardValidation.RequireName(legalName, nameof(legalName), 160)
            : GuardValidation.RequireName(displayName, nameof(displayName), 160);
        IsSupplier = isSupplier;
        IsCustomer = isCustomer;
        TaxRegistrationNpwp = GuardValidation.NormalizeOptional(npwp, 64, nameof(npwp));
        TaxPkpStatus = TaxPkpStatuses.Normalize(taxPkpStatus);
        EntityScopeMode = EntityScopeModes.Normalize(entityScopeMode);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Deactivate(DateTimeOffset atUtc)
    {
        if (Status == PartnerStatuses.Inactive)
        {
            return;
        }

        Status = PartnerStatuses.Inactive;
        DeactivatedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    public void Activate(DateTimeOffset atUtc)
    {
        if (Status == PartnerStatuses.Active)
        {
            return;
        }

        Status = PartnerStatuses.Active;
        DeactivatedAtUtc = null;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    private static void RequireAtLeastOneRole(bool isSupplier, bool isCustomer)
    {
        if (!isSupplier && !isCustomer)
        {
            throw new ArgumentException("Partner must be marked as Supplier, Customer, or both.");
        }
    }
}

public sealed class PartnerContact
{
    public Guid ContactId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartnerId { get; private set; }
    public string ContactName { get; private set; } = string.Empty;
    public string ContactRole { get; private set; } = PartnerContactRoles.Commercial;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }
    public string Status { get; private set; } = PartnerStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private PartnerContact()
    {
    }

    public static PartnerContact Create(
        Guid contactId,
        Guid tenantId,
        Guid partnerId,
        string contactName,
        string contactRole,
        string? email,
        string? phone,
        bool isPrimary,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(contactId, nameof(contactId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(partnerId, nameof(partnerId));

        return new PartnerContact
        {
            ContactId = contactId,
            TenantId = tenantId,
            PartnerId = partnerId,
            ContactName = GuardValidation.RequireName(contactName, nameof(contactName), 160),
            ContactRole = PartnerContactRoles.Normalize(contactRole),
            Email = GuardValidation.NormalizeOptional(email, 256, nameof(email)),
            Phone = GuardValidation.NormalizeOptional(phone, 64, nameof(phone)),
            IsPrimary = isPrimary,
            Status = PartnerStatuses.Active,
            CreatedAtUtc = nowUtc
        };
    }
}

public sealed class PartnerEntityProfile
{
    public Guid ProfileId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid PartnerId { get; private set; }
    public bool SupplierEligible { get; private set; }
    public bool CustomerEligible { get; private set; }
    public int DefaultPaymentTermsDays { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private PartnerEntityProfile()
    {
    }

    public static PartnerEntityProfile CreateForPartner(
        Guid profileId,
        Partner partner,
        Guid legalEntityId,
        bool supplierEligible,
        bool customerEligible,
        int defaultPaymentTermsDays,
        string? notes,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(profileId, nameof(profileId));
        ArgumentNullException.ThrowIfNull(partner);
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        ValidateCompatibilityAndTerms(partner, supplierEligible, customerEligible, defaultPaymentTermsDays);

        return new PartnerEntityProfile
        {
            ProfileId = profileId,
            TenantId = partner.TenantId,
            LegalEntityId = legalEntityId,
            PartnerId = partner.PartnerId,
            SupplierEligible = supplierEligible,
            CustomerEligible = customerEligible,
            DefaultPaymentTermsDays = defaultPaymentTermsDays,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateForPartner(
        Partner partner,
        bool supplierEligible,
        bool customerEligible,
        int defaultPaymentTermsDays,
        string? notes,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(partner);
        if (partner.TenantId != TenantId || partner.PartnerId != PartnerId)
        {
            throw new InvalidOperationException("Partner profile cannot cross tenant or partner boundary.");
        }

        ValidateCompatibilityAndTerms(partner, supplierEligible, customerEligible, defaultPaymentTermsDays);

        SupplierEligible = supplierEligible;
        CustomerEligible = customerEligible;
        DefaultPaymentTermsDays = defaultPaymentTermsDays;
        Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes));
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    private static void ValidateCompatibilityAndTerms(
        Partner partner,
        bool supplierEligible,
        bool customerEligible,
        int paymentTermsDays)
    {
        if (supplierEligible && !partner.IsSupplier)
        {
            throw new InvalidOperationException("partner_role_incompatible: Partner is not flagged as a Supplier.");
        }

        if (customerEligible && !partner.IsCustomer)
        {
            throw new InvalidOperationException("partner_role_incompatible: Partner is not flagged as a Customer.");
        }

        if (paymentTermsDays < 0 || paymentTermsDays > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermsDays), "Payment terms days must be between 0 and 365.");
        }
    }
}

public static class PartnerEligibilityEvaluator
{
    public static (bool SupplierEligible, bool CustomerEligible) Evaluate(
        Partner partner,
        PartnerEntityProfile? profile)
    {
        if (partner.Status != PartnerStatuses.Active)
        {
            return (false, false);
        }

        var supplierAllowed = partner.IsSupplier && EvaluateDirection(partner.EntityScopeMode, profile?.SupplierEligible);
        var customerAllowed = partner.IsCustomer && EvaluateDirection(partner.EntityScopeMode, profile?.CustomerEligible);
        return (supplierAllowed, customerAllowed);
    }

    private static bool EvaluateDirection(string scopeMode, bool? profileFlag) =>
        scopeMode switch
        {
            EntityScopeModes.AllEntities => profileFlag ?? true,
            EntityScopeModes.RestrictedEntities => profileFlag == true,
            _ => false
        };
}
