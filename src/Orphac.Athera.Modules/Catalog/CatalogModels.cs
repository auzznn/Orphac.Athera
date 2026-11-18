using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Catalog;

public static class CatalogStatuses
{
    public const string Active = MasterStatuses.Active;
    public const string Inactive = MasterStatuses.Inactive;
    public static readonly IReadOnlyList<string> All = MasterStatuses.All;
}

public static class ItemKinds
{
    public const string StockedGood = "STOCKED_GOOD";
    public const string NonStockService = "NON_STOCK_SERVICE";

    public static readonly IReadOnlyList<string> All = [StockedGood, NonStockService];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? StockedGood : raw.Trim().ToUpperInvariant();
        if (value is not (StockedGood or NonStockService))
        {
            throw new ArgumentException($"Unsupported item kind '{raw}'.", nameof(raw));
        }

        return value;
    }
}

public static class LotTrackingPolicies
{
    public const string None = "NONE";
    public const string Optional = "OPTIONAL";
    public const string Required = "REQUIRED";

    public static readonly IReadOnlyList<string> All = [None, Optional, Required];

    public static string Normalize(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? None : raw.Trim().ToUpperInvariant();
        if (value is not (None or Optional or Required))
        {
            throw new ArgumentException($"Unsupported lot tracking policy '{raw}'.", nameof(raw));
        }

        return value;
    }
}

/// <summary>
/// Tenant-scoped unit of measure defining explicit decimal precision scale (0 = discrete, 1..6 = continuous).
/// </summary>
public sealed class UnitOfMeasure
{
    public Guid UomId { get; private set; }
    public Guid TenantId { get; private set; }
    public string UomCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int QuantityPrecisionScale { get; private set; }
    public string Status { get; private set; } = CatalogStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private UnitOfMeasure()
    {
    }

    public static UnitOfMeasure Create(
        Guid uomId,
        Guid tenantId,
        string code,
        string name,
        int quantityPrecisionScale,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(uomId, nameof(uomId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));

        if (quantityPrecisionScale < 0 || quantityPrecisionScale > 6)
        {
            throw new ArgumentException("QuantityPrecisionScale must be between 0 and 6.", nameof(quantityPrecisionScale));
        }

        return new UnitOfMeasure
        {
            UomId = uomId,
            TenantId = tenantId,
            UomCode = GuardValidation.RequireMasterCode(code, nameof(code), minLength: 1, maxLength: 16),
            Name = GuardValidation.RequireName(name, nameof(name), 100),
            QuantityPrecisionScale = quantityPrecisionScale,
            Status = CatalogStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }
}

/// <summary>
/// Tenant-shared item catalogue definition. Contains zero stock balance or valuation fields.
/// </summary>
public sealed class Item
{
    public Guid ItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ItemCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ItemKind { get; private set; } = ItemKinds.StockedGood;
    public string LotTrackingPolicy { get; private set; } = LotTrackingPolicies.None;
    public Guid BaseUomId { get; private set; }
    public Guid DefaultPurchaseUomId { get; private set; }
    public Guid DefaultSalesUomId { get; private set; }
    public string EntityScopeMode { get; private set; } = EntityScopeModes.AllEntities;
    public string Status { get; private set; } = CatalogStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeactivatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private Item()
    {
    }

    public static Item Create(
        Guid itemId,
        Guid tenantId,
        string code,
        string name,
        string itemKind,
        Guid baseUomId,
        Guid? defaultPurchaseUomId,
        Guid? defaultSalesUomId,
        string entityScopeMode,
        DateTimeOffset nowUtc,
        string? lotTrackingPolicy = null)
    {
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(baseUomId, nameof(baseUomId));

        var normalizedKind = ItemKinds.Normalize(itemKind);
        var normalizedLotPolicy = LotTrackingPolicies.Normalize(lotTrackingPolicy);
        if (normalizedKind == ItemKinds.NonStockService && normalizedLotPolicy != LotTrackingPolicies.None)
        {
            throw new ArgumentException("Non-stock service items cannot enable lot tracking.", nameof(lotTrackingPolicy));
        }

        var resolvedPurchaseUomId = defaultPurchaseUomId is { } p && p != Guid.Empty ? p : baseUomId;
        var resolvedSalesUomId = defaultSalesUomId is { } s && s != Guid.Empty ? s : baseUomId;

        return new Item
        {
            ItemId = itemId,
            TenantId = tenantId,
            ItemCode = GuardValidation.RequireMasterCode(code, nameof(code)),
            Name = GuardValidation.RequireName(name, nameof(name), 200),
            ItemKind = normalizedKind,
            LotTrackingPolicy = normalizedLotPolicy,
            BaseUomId = baseUomId,
            DefaultPurchaseUomId = resolvedPurchaseUomId,
            DefaultSalesUomId = resolvedSalesUomId,
            EntityScopeMode = EntityScopeModes.Normalize(entityScopeMode),
            Status = CatalogStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateDetails(
        string name,
        string itemKind,
        Guid defaultPurchaseUomId,
        Guid defaultSalesUomId,
        string entityScopeMode,
        DateTimeOffset nowUtc,
        string? lotTrackingPolicy = null)
    {
        GuardValidation.RequireNonEmptyGuid(defaultPurchaseUomId, nameof(defaultPurchaseUomId));
        GuardValidation.RequireNonEmptyGuid(defaultSalesUomId, nameof(defaultSalesUomId));

        var normalizedKind = ItemKinds.Normalize(itemKind);
        var normalizedLotPolicy = lotTrackingPolicy is null
            ? LotTrackingPolicy
            : LotTrackingPolicies.Normalize(lotTrackingPolicy);
        if (normalizedKind == ItemKinds.NonStockService && normalizedLotPolicy != LotTrackingPolicies.None)
        {
            throw new ArgumentException("Non-stock service items cannot enable lot tracking.", nameof(lotTrackingPolicy));
        }

        Name = GuardValidation.RequireName(name, nameof(name), 200);
        ItemKind = normalizedKind;
        LotTrackingPolicy = normalizedLotPolicy;
        DefaultPurchaseUomId = defaultPurchaseUomId;
        DefaultSalesUomId = defaultSalesUomId;
        EntityScopeMode = EntityScopeModes.Normalize(entityScopeMode);
        UpdatedAtUtc = nowUtc;
        Version++;
    }

    public void Deactivate(DateTimeOffset atUtc)
    {
        if (Status == CatalogStatuses.Inactive)
        {
            return;
        }

        Status = CatalogStatuses.Inactive;
        DeactivatedAtUtc = atUtc;
        UpdatedAtUtc = atUtc;
        Version++;
    }

    public void Activate(DateTimeOffset atUtc)
    {
        if (Status == CatalogStatuses.Active)
        {
            return;
        }

        Status = CatalogStatuses.Active;
        DeactivatedAtUtc = null;
        UpdatedAtUtc = atUtc;
        Version++;
    }
}

public sealed class ItemUom
{
    public Guid ItemUomId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid UomId { get; private set; }
    public decimal FactorToBase { get; private set; }
    public string Status { get; private set; } = CatalogStatuses.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ItemUom()
    {
    }

    public static ItemUom Create(
        Guid itemUomId,
        Guid tenantId,
        Guid itemId,
        Guid uomId,
        decimal factorToBase,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(itemUomId, nameof(itemUomId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));
        GuardValidation.RequireNonEmptyGuid(uomId, nameof(uomId));

        if (factorToBase <= 0m)
        {
            throw new ArgumentException("FactorToBase must be strictly greater than zero.", nameof(factorToBase));
        }

        return new ItemUom
        {
            ItemUomId = itemUomId,
            TenantId = tenantId,
            ItemId = itemId,
            UomId = uomId,
            FactorToBase = decimal.Round(factorToBase, 6, MidpointRounding.AwayFromZero),
            Status = CatalogStatuses.Active,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void UpdateFactor(decimal factorToBase, string status, DateTimeOffset atUtc)
    {
        if (factorToBase <= 0m)
        {
            throw new ArgumentException("FactorToBase must be strictly greater than zero.", nameof(factorToBase));
        }

        FactorToBase = decimal.Round(factorToBase, 6, MidpointRounding.AwayFromZero);
        Status = MasterStatuses.Normalize(status);
        UpdatedAtUtc = atUtc;
        Version++;
    }
}

public sealed class ItemEntityPolicy
{
    public Guid PolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid ItemId { get; private set; }
    public bool PurchaseAllowed { get; private set; }
    public bool SalesAllowed { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private ItemEntityPolicy()
    {
    }

    public static ItemEntityPolicy Create(
        Guid policyId,
        Guid tenantId,
        Guid legalEntityId,
        Guid itemId,
        bool purchaseAllowed,
        bool salesAllowed,
        string? notes,
        DateTimeOffset nowUtc)
    {
        GuardValidation.RequireNonEmptyGuid(policyId, nameof(policyId));
        GuardValidation.RequireNonEmptyGuid(tenantId, nameof(tenantId));
        GuardValidation.RequireNonEmptyGuid(legalEntityId, nameof(legalEntityId));
        GuardValidation.RequireNonEmptyGuid(itemId, nameof(itemId));

        return new ItemEntityPolicy
        {
            PolicyId = policyId,
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            ItemId = itemId,
            PurchaseAllowed = purchaseAllowed,
            SalesAllowed = salesAllowed,
            Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes)),
            UpdatedAtUtc = nowUtc,
            Version = 1L
        };
    }

    public void Update(bool purchaseAllowed, bool salesAllowed, string? notes, DateTimeOffset nowUtc)
    {
        PurchaseAllowed = purchaseAllowed;
        SalesAllowed = salesAllowed;
        Notes = GuardValidation.NormalizeOptional(notes, 300, nameof(notes));
        UpdatedAtUtc = nowUtc;
        Version++;
    }
}

public static class ItemEligibilityEvaluator
{
    public static (bool PurchaseAllowed, bool SalesAllowed) Evaluate(Item item, ItemEntityPolicy? policy)
    {
        if (item.Status != CatalogStatuses.Active)
        {
            return (false, false);
        }

        return item.EntityScopeMode switch
        {
            EntityScopeModes.AllEntities => (policy?.PurchaseAllowed ?? true, policy?.SalesAllowed ?? true),
            EntityScopeModes.RestrictedEntities => (policy?.PurchaseAllowed == true, policy?.SalesAllowed == true),
            _ => (false, false)
        };
    }
}

public sealed record UomConversionResult(
    decimal InputQuantity,
    string InputUomCode,
    decimal FactorToBase,
    decimal ExactBaseQuantity,
    decimal ConvertedBaseQuantity,
    decimal RoundingResidual,
    string BaseUomCode,
    int BasePrecisionScale);

public sealed record ItemTransactionSnapshot(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string ItemKind,
    string BaseUomCode,
    string TransactionUomCode,
    decimal FactorToBase,
    decimal TransactionQuantity,
    decimal ConvertedBaseQuantity,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// Deterministic decimal-only UoM converter with discrete base enforcement and snapshot materialization.
/// </summary>
public static class UomConverter
{
    public static UomConversionResult ConvertToBase(
        Item item,
        UnitOfMeasure baseUom,
        UnitOfMeasure sourceUom,
        ItemUom? conversion,
        decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(baseUom);
        ArgumentNullException.ThrowIfNull(sourceUom);

        if (quantity <= 0m)
        {
            throw new ArgumentException("invalid_quantity: Quantity must be greater than zero.", nameof(quantity));
        }

        ValidateSameTenantAndBaseUom(item, baseUom, sourceUom);
        var factor = ResolveFactorToBase(item, sourceUom, conversion);

        var exactBase = quantity * factor;
        if (baseUom.QuantityPrecisionScale == 0 && exactBase != decimal.Truncate(exactBase))
        {
            throw new InvalidOperationException(
                $"fractional_discrete_base_quantity: Converting {quantity} {sourceUom.UomCode} by factor {factor} produces fractional {baseUom.UomCode} ({exactBase}).");
        }

        var converted = decimal.Round(exactBase, baseUom.QuantityPrecisionScale, MidpointRounding.AwayFromZero);
        var residual = exactBase - converted;

        return new UomConversionResult(
            quantity,
            sourceUom.UomCode,
            factor,
            exactBase,
            converted,
            residual,
            baseUom.UomCode,
            baseUom.QuantityPrecisionScale);
    }

    public static decimal ConvertFromBase(
        Item item,
        UnitOfMeasure baseUom,
        UnitOfMeasure targetUom,
        ItemUom? conversion,
        decimal baseQuantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(baseUom);
        ArgumentNullException.ThrowIfNull(targetUom);

        if (baseQuantity <= 0m)
        {
            throw new ArgumentException("invalid_quantity: Base quantity must be greater than zero.", nameof(baseQuantity));
        }

        ValidateSameTenantAndBaseUom(item, baseUom, targetUom);
        var factor = ResolveFactorToBase(item, targetUom, conversion);

        var exactTarget = baseQuantity / factor;
        if (targetUom.QuantityPrecisionScale == 0 && exactTarget != decimal.Truncate(exactTarget))
        {
            throw new InvalidOperationException(
                $"fractional_discrete_base_quantity: Dividing {baseQuantity} {baseUom.UomCode} by factor {factor} produces fractional discrete {targetUom.UomCode} ({exactTarget}).");
        }

        return decimal.Round(exactTarget, targetUom.QuantityPrecisionScale, MidpointRounding.AwayFromZero);
    }

    public static ItemTransactionSnapshot CaptureSnapshot(
        Item item,
        UnitOfMeasure baseUom,
        UnitOfMeasure sourceUom,
        ItemUom? conversion,
        decimal quantity,
        DateTimeOffset capturedAtUtc)
    {
        var converted = ConvertToBase(item, baseUom, sourceUom, conversion, quantity);
        return new ItemTransactionSnapshot(
            item.ItemId,
            item.ItemCode,
            item.Name,
            item.ItemKind,
            baseUom.UomCode,
            sourceUom.UomCode,
            converted.FactorToBase,
            quantity,
            converted.ConvertedBaseQuantity,
            capturedAtUtc);
    }

    private static void ValidateSameTenantAndBaseUom(Item item, UnitOfMeasure baseUom, UnitOfMeasure otherUom)
    {
        if (item.TenantId != baseUom.TenantId || item.TenantId != otherUom.TenantId)
        {
            throw new InvalidOperationException("Item and UoM definitions must belong to the same tenant.");
        }

        if (item.BaseUomId != baseUom.UomId)
        {
            throw new InvalidOperationException("Provided base UoM does not match Item.BaseUomId.");
        }
    }

    private static decimal ResolveFactorToBase(Item item, UnitOfMeasure uom, ItemUom? conversion)
    {
        if (uom.UomId == item.BaseUomId)
        {
            return 1m;
        }

        if (conversion is null
            || conversion.TenantId != item.TenantId
            || conversion.ItemId != item.ItemId
            || conversion.UomId != uom.UomId
            || conversion.Status != CatalogStatuses.Active)
        {
            throw new InvalidOperationException(
                $"unsupported_uom_conversion: No active conversion exists between item '{item.ItemCode}' and UoM '{uom.UomCode}'.");
        }

        return conversion.FactorToBase;
    }
}
