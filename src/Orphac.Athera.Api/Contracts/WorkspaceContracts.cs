namespace Orphac.Athera.Api.Contracts;

public sealed record TenantSummaryDto(
    Guid TenantId,
    string Code,
    string Name,
    string Status);

public sealed record LegalEntityDto(
    Guid LegalEntityId,
    Guid TenantId,
    string Code,
    string Name,
    string FunctionalCurrencyCode,
    string CountryCode,
    string TimeZoneId,
    string? TaxRegistrationNpwp,
    string Status,
    IReadOnlyList<string> Permissions);

public sealed record AuthorizedTenantDto(
    Guid TenantId,
    string Code,
    string Name,
    string Status,
    IReadOnlyList<LegalEntityDto> Entities);

public sealed record WorkspacesResponse(
    IReadOnlyList<AuthorizedTenantDto> Tenants,
    int TotalAuthorizedEntities,
    DateTimeOffset EvaluatedAtUtc);

public sealed record TenantEntitiesResponse(
    TenantSummaryDto Tenant,
    IReadOnlyList<LegalEntityDto> Entities,
    DateTimeOffset EvaluatedAtUtc);

public sealed record ModuleNavigationItemDto(
    string Key,
    string Status,
    bool Permitted,
    string? RequiredPermission);

public sealed record EntityWorkspaceDetailResponse(
    TenantSummaryDto Tenant,
    LegalEntityDto Entity,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<ModuleNavigationItemDto> NavigationCapabilities,
    DateTimeOffset ServerTimestampUtc);
