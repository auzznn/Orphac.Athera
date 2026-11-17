namespace Orphac.Athera.Modules.Platform;

/// <summary>
/// Statutory accounting and operational entity strictly owned by one tenant.
/// </summary>
public sealed class LegalEntity
{
    public Guid LegalEntityId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? TaxRegistrationNpwp { get; private set; }
    public string FunctionalCurrencyCode { get; private set; } = "IDR";
    public string CountryCode { get; private set; } = "ID";
    public string TimeZoneId { get; private set; } = "Asia/Jakarta";
    public string Status { get; private set; } = LegalEntityStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    public bool IsActive => Status == LegalEntityStatuses.Active;

    private LegalEntity()
    {
    }

    public static LegalEntity Create(
        Guid legalEntityId,
        Guid tenantId,
        string code,
        string name,
        string? taxRegistrationNpwp,
        string? functionalCurrencyCode,
        string? countryCode,
        string? timeZoneId,
        DateTimeOffset nowUtc)
    {
        if (legalEntityId == Guid.Empty)
        {
            throw new ArgumentException("LegalEntityId must not be empty.", nameof(legalEntityId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        var trimmedNpwp = string.IsNullOrWhiteSpace(taxRegistrationNpwp) ? null : taxRegistrationNpwp.Trim();
        if (trimmedNpwp is { Length: > 64 })
        {
            throw new ArgumentException("TaxRegistrationNpwp must not exceed 64 characters.", nameof(taxRegistrationNpwp));
        }

        var tz = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Jakarta" : timeZoneId.Trim();
        if (tz.Length > 64)
        {
            throw new ArgumentException("TimeZoneId must not exceed 64 characters.", nameof(timeZoneId));
        }

        return new LegalEntity
        {
            LegalEntityId = legalEntityId,
            TenantId = tenantId,
            Code = GuardValidation.RequireCode(code, nameof(code)),
            Name = GuardValidation.RequireName(name, nameof(name)),
            TaxRegistrationNpwp = trimmedNpwp,
            FunctionalCurrencyCode = GuardValidation.RequireExactIsoCode(functionalCurrencyCode, "IDR", 3, nameof(functionalCurrencyCode)),
            CountryCode = GuardValidation.RequireExactIsoCode(countryCode, "ID", 2, nameof(countryCode)),
            TimeZoneId = tz,
            Status = LegalEntityStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void Activate(DateTimeOffset atUtc) => Transition(LegalEntityStatuses.Active, atUtc);
    public void Suspend(DateTimeOffset atUtc) => Transition(LegalEntityStatuses.Suspended, atUtc);
    public void Archive(DateTimeOffset atUtc) => Transition(LegalEntityStatuses.Archived, atUtc);

    private void Transition(string nextStatus, DateTimeOffset atUtc)
    {
        if (Status == nextStatus)
        {
            return;
        }

        Status = nextStatus;
        UpdatedAtUtc = atUtc;
        Version++;
    }
}
