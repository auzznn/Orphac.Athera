namespace Orphac.Athera.Api.Contracts;

public sealed record PartnerContactDto(
    Guid ContactId,
    string ContactName,
    string ContactRole,
    string? Email,
    string? Phone,
    bool IsPrimary,
    string Status);

public sealed record PartnerEntityProfileDto(
    Guid ProfileId,
    Guid LegalEntityId,
    bool SupplierEligible,
    bool CustomerEligible,
    int DefaultPaymentTermsDays,
    string? Notes,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record PartnerListItemDto(
    Guid PartnerId,
    string PartnerCode,
    string LegalName,
    string DisplayName,
    bool IsSupplier,
    bool IsCustomer,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    string EntityScopeMode,
    string Status,
    bool EffectiveSupplierEligible,
    bool EffectiveCustomerEligible,
    int ContactCount,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record PartnerDetailDto(
    Guid PartnerId,
    string PartnerCode,
    string LegalName,
    string DisplayName,
    bool IsSupplier,
    bool IsCustomer,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    string EntityScopeMode,
    string Status,
    bool EffectiveSupplierEligible,
    bool EffectiveCustomerEligible,
    PartnerEntityProfileDto? CurrentEntityProfile,
    IReadOnlyList<PartnerContactDto> Contacts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc,
    long Version);

public sealed record PagedPartnersResponse(
    IReadOnlyList<PartnerListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CreatePartnerRequest(
    string PartnerCode,
    string LegalName,
    string DisplayName,
    bool IsSupplier,
    bool IsCustomer,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    string EntityScopeMode);

public sealed record UpdatePartnerRequest(
    string LegalName,
    string DisplayName,
    bool IsSupplier,
    bool IsCustomer,
    string? TaxRegistrationNpwp,
    string TaxPkpStatus,
    string EntityScopeMode,
    string? Status,
    long ExpectedVersion);

public sealed record CreatePartnerContactRequest(
    string ContactName,
    string ContactRole,
    string? Email,
    string? Phone,
    bool IsPrimary);

public sealed record UpsertPartnerEntityProfileRequest(
    bool SupplierEligible,
    bool CustomerEligible,
    int DefaultPaymentTermsDays,
    string? Notes);

public sealed record UnitOfMeasureDto(
    Guid UomId,
    string UomCode,
    string Name,
    int QuantityPrecisionScale,
    string Status,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record UomListResponse(IReadOnlyList<UnitOfMeasureDto> Items);

public sealed record CreateUomRequest(
    string UomCode,
    string Name,
    int QuantityPrecisionScale);

public sealed record ItemUomDto(
    Guid ItemUomId,
    Guid UomId,
    string UomCode,
    string UomName,
    int QuantityPrecisionScale,
    decimal FactorToBase,
    string Status,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record ItemEntityPolicyDto(
    Guid PolicyId,
    Guid LegalEntityId,
    bool PurchaseAllowed,
    bool SalesAllowed,
    string? Notes,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record ItemListItemDto(
    Guid ItemId,
    string ItemCode,
    string Name,
    string ItemKind,
    Guid BaseUomId,
    string BaseUomCode,
    int BasePrecisionScale,
    Guid DefaultPurchaseUomId,
    string DefaultPurchaseUomCode,
    Guid DefaultSalesUomId,
    string DefaultSalesUomCode,
    string EntityScopeMode,
    string Status,
    bool EffectivePurchaseAllowed,
    bool EffectiveSalesAllowed,
    int ConversionCount,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public sealed record ItemDetailDto(
    Guid ItemId,
    string ItemCode,
    string Name,
    string ItemKind,
    Guid BaseUomId,
    string BaseUomCode,
    int BasePrecisionScale,
    Guid DefaultPurchaseUomId,
    string DefaultPurchaseUomCode,
    Guid DefaultSalesUomId,
    string DefaultSalesUomCode,
    string EntityScopeMode,
    string Status,
    bool EffectivePurchaseAllowed,
    bool EffectiveSalesAllowed,
    ItemEntityPolicyDto? CurrentEntityPolicy,
    IReadOnlyList<ItemUomDto> Conversions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DeactivatedAtUtc,
    long Version);

public sealed record PagedItemsResponse(
    IReadOnlyList<ItemListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CreateItemRequest(
    string ItemCode,
    string Name,
    string ItemKind,
    Guid BaseUomId,
    Guid? DefaultPurchaseUomId,
    Guid? DefaultSalesUomId,
    string EntityScopeMode);

public sealed record UpdateItemRequest(
    string Name,
    string ItemKind,
    Guid DefaultPurchaseUomId,
    Guid DefaultSalesUomId,
    string EntityScopeMode,
    string? Status,
    long ExpectedVersion);

public sealed record UpsertItemUomRequest(
    Guid UomId,
    decimal FactorToBase,
    string Status);

public sealed record UpsertItemEntityPolicyRequest(
    bool PurchaseAllowed,
    bool SalesAllowed,
    string? Notes);

public sealed record ConvertQuantityPreviewRequest(
    Guid SourceUomId,
    decimal Quantity);

public sealed record ConvertQuantityPreviewResponse(
    Guid ItemId,
    string ItemCode,
    decimal InputQuantity,
    string InputUomCode,
    decimal FactorToBase,
    decimal ExactBaseQuantity,
    decimal ConvertedBaseQuantity,
    decimal RoundingResidual,
    string BaseUomCode,
    int BasePrecisionScale,
    decimal RoundTripInputQuantity,
    DateTimeOffset CapturedAtUtc);
