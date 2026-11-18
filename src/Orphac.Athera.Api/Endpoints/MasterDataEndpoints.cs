using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        MapPartnerRoutes(app);
        MapCatalogRoutes(app);
        return app;
    }

    private static void MapPartnerRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? q,
            [FromQuery] string? role,
            [FromQuery] string? status,
            [FromQuery] bool? eligibleOnly,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.PartnersRead, PermissionCodes.PartnersManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var normalizedPage = page is null or < 1 ? 1 : page.Value;
                    var normalizedSize = pageSize is null or < 1 or > 100 ? 25 : pageSize.Value;
                    var search = q?.Trim().ToUpperInvariant();
                    var roleFilter = role?.Trim().ToUpperInvariant();
                    var statusFilter = status?.Trim().ToUpperInvariant();

                    var query = db.Partners.AsNoTracking().Where(p => p.TenantId == tenantId);
                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(p =>
                            p.PartnerCode.ToUpper().Contains(search)
                            || p.LegalName.ToUpper().Contains(search)
                            || p.DisplayName.ToUpper().Contains(search));
                    }

                    if (roleFilter == "SUPPLIER")
                    {
                        query = query.Where(p => p.IsSupplier);
                    }
                    else if (roleFilter == "CUSTOMER")
                    {
                        query = query.Where(p => p.IsCustomer);
                    }

                    if (statusFilter is PartnerStatuses.Active or PartnerStatuses.Inactive)
                    {
                        query = query.Where(p => p.Status == statusFilter);
                    }

                    var partners = await query
                        .OrderBy(p => p.PartnerCode)
                        .ToListAsync(innerCt);

                    var partnerIds = partners.Select(p => p.PartnerId).ToList();
                    var profiles = await db.PartnerEntityProfiles.AsNoTracking()
                        .Where(x => x.TenantId == tenantId && x.LegalEntityId == entityId && partnerIds.Contains(x.PartnerId))
                        .ToDictionaryAsync(x => x.PartnerId, innerCt);

                    var contactCounts = await db.PartnerContacts.AsNoTracking()
                        .Where(c => c.TenantId == tenantId && partnerIds.Contains(c.PartnerId))
                        .GroupBy(c => c.PartnerId)
                        .Select(g => new { PartnerId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.PartnerId, x => x.Count, innerCt);

                    var evaluated = partners
                        .Select(p =>
                        {
                            profiles.TryGetValue(p.PartnerId, out var profile);
                            var (supEligible, cusEligible) = PartnerEligibilityEvaluator.Evaluate(p, profile);
                            return new PartnerListItemDto(
                                p.PartnerId,
                                p.PartnerCode,
                                p.LegalName,
                                p.DisplayName,
                                p.IsSupplier,
                                p.IsCustomer,
                                p.TaxRegistrationNpwp,
                                p.TaxPkpStatus,
                                p.EntityScopeMode,
                                p.Status,
                                supEligible,
                                cusEligible,
                                contactCounts.GetValueOrDefault(p.PartnerId, 0),
                                p.UpdatedAtUtc,
                                p.Version);
                        });

                    if (eligibleOnly == true)
                    {
                        evaluated = evaluated.Where(x => x.EffectiveSupplierEligible || x.EffectiveCustomerEligible);
                    }

                    var allFiltered = evaluated.ToList();
                    var total = allFiltered.Count;
                    var paged = allFiltered
                        .Skip((normalizedPage - 1) * normalizedSize)
                        .Take(normalizedSize)
                        .ToList();

                    return Results.Ok(new PagedPartnersResponse(paged, total, normalizedPage, normalizedSize));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListPartners")
        .Produces<PagedPartnersResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] CreatePartnerRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.PartnersManage,
                "duplicate_partner_code",
                "Partner code already exists in this tenant.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    Partner partner;
                    try
                    {
                        partner = Partner.Create(
                            Guid.NewGuid(),
                            tenantId,
                            request.PartnerCode,
                            request.LegalName,
                            request.DisplayName,
                            request.IsSupplier,
                            request.IsCustomer,
                            request.TaxRegistrationNpwp,
                            request.TaxPkpStatus,
                            request.EntityScopeMode,
                            nowUtc);
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_partner", ex.Message);
                    }

                    db.Partners.Add(partner);
                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "PARTNER_CREATED",
                        nameof(Partner),
                        partner.PartnerId.ToString("D"),
                        $"Created partner {partner.PartnerCode} ({partner.LegalName})",
                        "partners.partner.created",
                        $"partner-created:{partner.PartnerId:D}",
                        new { partner.PartnerId, partner.PartnerCode, partner.IsSupplier, partner.IsCustomer, partner.EntityScopeMode },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildPartnerDetailAsync(db, tenantId, entityId, partner.PartnerId, innerCt);
                    return Results.Created($"/api/workspaces/{tenantId:D}/entities/{entityId:D}/partners/{partner.PartnerId:D}", dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreatePartner")
        .Produces<PartnerDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners/{partnerId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid partnerId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.PartnersRead, PermissionCodes.PartnersManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var dto = await BuildPartnerDetailAsync(db, tenantId, entityId, partnerId, innerCt);
                    return dto is null
                        ? EndpointSupport.NotFound(httpContext, "partner_not_found", "Partner was not found in this tenant.")
                        : Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetPartnerDetail")
        .Produces<PartnerDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners/{partnerId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid partnerId,
            [FromBody] UpdatePartnerRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.PartnersManage,
                "duplicate_partner_code",
                "Partner conflict detected.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var partner = await db.Partners.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == partnerId, innerCt);
                    if (partner is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "partner_not_found", "Partner was not found in this tenant.");
                    }

                    if (request.ExpectedVersion > 0 && request.ExpectedVersion != partner.Version)
                    {
                        return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Partner was updated concurrently; reload and retry.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    try
                    {
                        partner.UpdateDetails(
                            request.LegalName,
                            request.DisplayName,
                            request.IsSupplier,
                            request.IsCustomer,
                            request.TaxRegistrationNpwp,
                            request.TaxPkpStatus,
                            request.EntityScopeMode,
                            nowUtc);

                        if (!string.IsNullOrWhiteSpace(request.Status))
                        {
                            var normalizedStatus = MasterStatuses.Normalize(request.Status);
                            if (normalizedStatus == PartnerStatuses.Inactive)
                            {
                                partner.Deactivate(nowUtc);
                            }
                            else
                            {
                                partner.Activate(nowUtc);
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_partner", ex.Message);
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        partner.Status == PartnerStatuses.Inactive ? "PARTNER_DEACTIVATED" : "PARTNER_UPDATED",
                        nameof(Partner),
                        partner.PartnerId.ToString("D"),
                        $"Updated partner {partner.PartnerCode} (Status={partner.Status}, Scope={partner.EntityScopeMode})",
                        "partners.partner.updated",
                        $"partner-updated:{partner.PartnerId:D}:v{partner.Version}",
                        new { partner.PartnerId, partner.PartnerCode, partner.Status, partner.EntityScopeMode, partner.Version },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildPartnerDetailAsync(db, tenantId, entityId, partner.PartnerId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdatePartner")
        .Produces<PartnerDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners/{partnerId:guid}/contacts", async (
            Guid tenantId,
            Guid entityId,
            Guid partnerId,
            [FromBody] CreatePartnerContactRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.PartnersManage,
                "duplicate_partner_contact",
                "Duplicate partner contact.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var partner = await db.Partners.AsNoTracking().SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == partnerId, innerCt);
                    if (partner is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "partner_not_found", "Partner was not found in this tenant.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    PartnerContact contact;
                    try
                    {
                        contact = PartnerContact.Create(
                            Guid.NewGuid(),
                            tenantId,
                            partnerId,
                            request.ContactName,
                            request.ContactRole,
                            request.Email,
                            request.Phone,
                            request.IsPrimary,
                            nowUtc);
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_partner_contact", ex.Message);
                    }

                    db.PartnerContacts.Add(contact);
                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "PARTNER_CONTACT_ADDED",
                        nameof(PartnerContact),
                        contact.ContactId.ToString("D"),
                        $"Added {contact.ContactRole} contact {contact.ContactName} for partner {partner.PartnerCode}",
                        "partners.contact.added",
                        $"partner-contact:{contact.ContactId:D}",
                        new { partnerId, contact.ContactId, contact.ContactRole },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildPartnerDetailAsync(db, tenantId, entityId, partnerId, innerCt);
                    return Results.Created($"/api/workspaces/{tenantId:D}/entities/{entityId:D}/partners/{partnerId:D}", dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreatePartnerContact")
        .Produces<PartnerDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/partners/{partnerId:guid}/entity-profile", async (
            Guid tenantId,
            Guid entityId,
            Guid partnerId,
            [FromBody] UpsertPartnerEntityProfileRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.PartnersManage,
                "duplicate_partner_entity_profile",
                "Partner entity profile conflict detected.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var partner = await db.Partners.AsNoTracking().SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == partnerId, innerCt);
                    if (partner is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "partner_not_found", "Partner was not found in this tenant.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    var existing = await db.PartnerEntityProfiles.SingleOrDefaultAsync(
                        x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.PartnerId == partnerId,
                        innerCt);

                    try
                    {
                        if (existing is null)
                        {
                            existing = PartnerEntityProfile.CreateForPartner(
                                Guid.NewGuid(),
                                partner,
                                entityId,
                                request.SupplierEligible,
                                request.CustomerEligible,
                                request.DefaultPaymentTermsDays,
                                request.Notes,
                                nowUtc);
                            db.PartnerEntityProfiles.Add(existing);
                        }
                        else
                        {
                            existing.UpdateForPartner(
                                partner,
                                request.SupplierEligible,
                                request.CustomerEligible,
                                request.DefaultPaymentTermsDays,
                                request.Notes,
                                nowUtc);
                        }
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        var errorCode = ex.Message.StartsWith("partner_role_incompatible", StringComparison.Ordinal)
                            ? "partner_role_incompatible"
                            : "invalid_partner_profile";
                        return EndpointSupport.BadRequest(httpContext, errorCode, ex.Message);
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "PARTNER_ENTITY_PROFILE_UPDATED",
                        nameof(PartnerEntityProfile),
                        existing.ProfileId.ToString("D"),
                        $"Updated {scope.EntityScope.Entity.Code} profile for partner {partner.PartnerCode} (Supplier={existing.SupplierEligible}, Customer={existing.CustomerEligible})",
                        "partners.profile.updated",
                        $"partner-profile:{existing.ProfileId:D}:v{existing.Version}",
                        new { partnerId, entityId, existing.SupplierEligible, existing.CustomerEligible, existing.DefaultPaymentTermsDays },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildPartnerDetailAsync(db, tenantId, entityId, partnerId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpsertPartnerEntityProfile")
        .Produces<PartnerDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static void MapCatalogRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/uoms", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.CatalogRead, PermissionCodes.CatalogManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var items = await db.UnitsOfMeasure.AsNoTracking()
                        .Where(u => u.TenantId == tenantId)
                        .OrderBy(u => u.UomCode)
                        .Select(u => new UnitOfMeasureDto(
                            u.UomId,
                            u.UomCode,
                            u.Name,
                            u.QuantityPrecisionScale,
                            u.Status,
                            u.UpdatedAtUtc,
                            u.Version))
                        .ToListAsync(innerCt);

                    return Results.Ok(new UomListResponse(items));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListUnitsOfMeasure")
        .Produces<UomListResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/uoms", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] CreateUomRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.CatalogManage,
                "duplicate_uom_code",
                "Unit of measure code already exists in this tenant.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    UnitOfMeasure uom;
                    try
                    {
                        uom = UnitOfMeasure.Create(
                            Guid.NewGuid(),
                            tenantId,
                            request.UomCode,
                            request.Name,
                            request.QuantityPrecisionScale,
                            nowUtc);
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_uom", ex.Message);
                    }

                    db.UnitsOfMeasure.Add(uom);
                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "UOM_CREATED",
                        nameof(UnitOfMeasure),
                        uom.UomId.ToString("D"),
                        $"Created UoM {uom.UomCode} ({uom.Name}, scale {uom.QuantityPrecisionScale})",
                        "catalog.uom.created",
                        $"uom-created:{uom.UomId:D}",
                        new { uom.UomId, uom.UomCode, uom.QuantityPrecisionScale },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    return Results.Created(
                        $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/catalog/uoms",
                        new UnitOfMeasureDto(uom.UomId, uom.UomCode, uom.Name, uom.QuantityPrecisionScale, uom.Status, uom.UpdatedAtUtc, uom.Version));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateUnitOfMeasure")
        .Produces<UnitOfMeasureDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items", async (
            Guid tenantId,
            Guid entityId,
            [FromQuery] string? q,
            [FromQuery] string? itemKind,
            [FromQuery] string? status,
            [FromQuery] bool? eligibleOnly,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.CatalogRead, PermissionCodes.CatalogManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var normalizedPage = page is null or < 1 ? 1 : page.Value;
                    var normalizedSize = pageSize is null or < 1 or > 100 ? 25 : pageSize.Value;
                    var search = q?.Trim().ToUpperInvariant();
                    var kindFilter = itemKind?.Trim().ToUpperInvariant();
                    var statusFilter = status?.Trim().ToUpperInvariant();

                    var query = db.Items.AsNoTracking().Where(i => i.TenantId == tenantId);
                    if (!string.IsNullOrEmpty(search))
                    {
                        query = query.Where(i => i.ItemCode.ToUpper().Contains(search) || i.Name.ToUpper().Contains(search));
                    }

                    if (kindFilter is ItemKinds.StockedGood or ItemKinds.NonStockService)
                    {
                        query = query.Where(i => i.ItemKind == kindFilter);
                    }

                    if (statusFilter is CatalogStatuses.Active or CatalogStatuses.Inactive)
                    {
                        query = query.Where(i => i.Status == statusFilter);
                    }

                    var items = await query.OrderBy(i => i.ItemCode).ToListAsync(innerCt);
                    var itemIds = items.Select(i => i.ItemId).ToList();

                    var uoms = await db.UnitsOfMeasure.AsNoTracking()
                        .Where(u => u.TenantId == tenantId)
                        .ToDictionaryAsync(u => u.UomId, innerCt);

                    var policies = await db.ItemEntityPolicies.AsNoTracking()
                        .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && itemIds.Contains(p.ItemId))
                        .ToDictionaryAsync(p => p.ItemId, innerCt);

                    var convCounts = await db.ItemUomConversions.AsNoTracking()
                        .Where(c => c.TenantId == tenantId && itemIds.Contains(c.ItemId) && c.Status == CatalogStatuses.Active)
                        .GroupBy(c => c.ItemId)
                        .Select(g => new { ItemId = g.Key, Count = g.Count() })
                        .ToDictionaryAsync(x => x.ItemId, x => x.Count, innerCt);

                    var evaluated = items.Select(i =>
                    {
                        policies.TryGetValue(i.ItemId, out var policy);
                        var (purchaseAllowed, salesAllowed) = ItemEligibilityEvaluator.Evaluate(i, policy);
                        var baseUom = uoms[i.BaseUomId];
                        var purchUom = uoms[i.DefaultPurchaseUomId];
                        var salesUom = uoms[i.DefaultSalesUomId];

                        return new ItemListItemDto(
                            i.ItemId,
                            i.ItemCode,
                            i.Name,
                            i.ItemKind,
                            i.LotTrackingPolicy,
                            i.BaseUomId,
                            baseUom.UomCode,
                            baseUom.QuantityPrecisionScale,
                            i.DefaultPurchaseUomId,
                            purchUom.UomCode,
                            i.DefaultSalesUomId,
                            salesUom.UomCode,
                            i.EntityScopeMode,
                            i.Status,
                            purchaseAllowed,
                            salesAllowed,
                            convCounts.GetValueOrDefault(i.ItemId, 0),
                            i.UpdatedAtUtc,
                            i.Version);
                    });

                    if (eligibleOnly == true)
                    {
                        evaluated = evaluated.Where(x => x.EffectivePurchaseAllowed || x.EffectiveSalesAllowed);
                    }

                    var filtered = evaluated.ToList();
                    var total = filtered.Count;
                    var paged = filtered.Skip((normalizedPage - 1) * normalizedSize).Take(normalizedSize).ToList();
                    return Results.Ok(new PagedItemsResponse(paged, total, normalizedPage, normalizedSize));
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ListCatalogItems")
        .Produces<PagedItemsResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items", async (
            Guid tenantId,
            Guid entityId,
            [FromBody] CreateItemRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.CatalogManage,
                "duplicate_item_code",
                "Item code already exists in this tenant.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var requestedUomIds = new[]
                    {
                        request.BaseUomId,
                        request.DefaultPurchaseUomId ?? request.BaseUomId,
                        request.DefaultSalesUomId ?? request.BaseUomId
                    }.Distinct().ToList();

                    var knownUomsCount = await db.UnitsOfMeasure.AsNoTracking()
                        .CountAsync(u => u.TenantId == tenantId && requestedUomIds.Contains(u.UomId), innerCt);
                    if (knownUomsCount != requestedUomIds.Count)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_uom", "One or more specified units of measure do not belong to this tenant.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    Item item;
                    try
                    {
                        item = Item.Create(
                            Guid.NewGuid(),
                            tenantId,
                            request.ItemCode,
                            request.Name,
                            request.ItemKind,
                            request.BaseUomId,
                            request.DefaultPurchaseUomId,
                            request.DefaultSalesUomId,
                            request.EntityScopeMode,
                            nowUtc,
                            request.LotTrackingPolicy);
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_item", ex.Message);
                    }

                    db.Items.Add(item);
                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "ITEM_CREATED",
                        nameof(Item),
                        item.ItemId.ToString("D"),
                        $"Created item {item.ItemCode} ({item.Name})",
                        "catalog.item.created",
                        $"item-created:{item.ItemId:D}",
                        new { item.ItemId, item.ItemCode, item.ItemKind, item.BaseUomId, item.EntityScopeMode },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildItemDetailAsync(db, tenantId, entityId, item.ItemId, innerCt);
                    return Results.Created($"/api/workspaces/{tenantId:D}/entities/{entityId:D}/catalog/items/{item.ItemId:D}", dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("CreateCatalogItem")
        .Produces<ItemDetailDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items/{itemId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.CatalogRead, PermissionCodes.CatalogManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var dto = await BuildItemDetailAsync(db, tenantId, entityId, itemId, innerCt);
                    return dto is null
                        ? EndpointSupport.NotFound(httpContext, "item_not_found", "Item was not found in this tenant.")
                        : Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("GetCatalogItemDetail")
        .Produces<ItemDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items/{itemId:guid}", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            [FromBody] UpdateItemRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.CatalogManage,
                "duplicate_item_code",
                "Item conflict detected.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var item = await db.Items.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, innerCt);
                    if (item is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "item_not_found", "Item was not found in this tenant.");
                    }

                    if (request.ExpectedVersion > 0 && request.ExpectedVersion != item.Version)
                    {
                        return EndpointSupport.Conflict(httpContext, "concurrency_conflict", "Item was updated concurrently; reload and retry.");
                    }

                    var uomIds = new[] { request.DefaultPurchaseUomId, request.DefaultSalesUomId }.Distinct().ToList();
                    var knownCount = await db.UnitsOfMeasure.AsNoTracking()
                        .CountAsync(u => u.TenantId == tenantId && uomIds.Contains(u.UomId), innerCt);
                    if (knownCount != uomIds.Count)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_uom", "Selected default purchase/sales unit of measure does not belong to this tenant.");
                    }

                    if (!string.IsNullOrWhiteSpace(request.LotTrackingPolicy)
                        && LotTrackingPolicies.Normalize(request.LotTrackingPolicy) == LotTrackingPolicies.Required
                        && item.LotTrackingPolicy != LotTrackingPolicies.Required)
                    {
                        var hasUnlottedStock = await db.StockLocationBalances.AsNoTracking()
                            .AnyAsync(b => b.TenantId == tenantId && b.ItemId == itemId && b.StockLotId == null && b.OnHandBaseQuantity > 0m, innerCt);
                        if (hasUnlottedStock)
                        {
                            return EndpointSupport.Conflict(
                                httpContext,
                                "unlotted_stock_prevents_required_lot_policy",
                                "Cannot enforce REQUIRED lot tracking while unlotted stock balances remain on hand.");
                        }
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    try
                    {
                        item.UpdateDetails(
                            request.Name,
                            request.ItemKind,
                            request.DefaultPurchaseUomId,
                            request.DefaultSalesUomId,
                            request.EntityScopeMode,
                            nowUtc,
                            request.LotTrackingPolicy);

                        if (!string.IsNullOrWhiteSpace(request.Status))
                        {
                            var normalizedStatus = MasterStatuses.Normalize(request.Status);
                            if (normalizedStatus == CatalogStatuses.Inactive)
                            {
                                item.Deactivate(nowUtc);
                            }
                            else
                            {
                                item.Activate(nowUtc);
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_item", ex.Message);
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        item.Status == CatalogStatuses.Inactive ? "ITEM_DEACTIVATED" : "ITEM_UPDATED",
                        nameof(Item),
                        item.ItemId.ToString("D"),
                        $"Updated item {item.ItemCode} (Status={item.Status}, Scope={item.EntityScopeMode})",
                        "catalog.item.updated",
                        $"item-updated:{item.ItemId:D}:v{item.Version}",
                        new { item.ItemId, item.ItemCode, item.Status, item.EntityScopeMode, item.Version },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildItemDetailAsync(db, tenantId, entityId, item.ItemId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpdateCatalogItem")
        .Produces<ItemDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items/{itemId:guid}/conversions", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            [FromBody] UpsertItemUomRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.CatalogManage,
                "duplicate_item_uom",
                "UoM conversion conflict detected.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, innerCt);
                    if (item is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "item_not_found", "Item was not found in this tenant.");
                    }

                    if (request.UomId == item.BaseUomId)
                    {
                        return EndpointSupport.BadRequest(httpContext, "base_uom_conversion_not_needed", "Base UoM always has implicit conversion factor 1.");
                    }

                    var uom = await db.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(u => u.TenantId == tenantId && u.UomId == request.UomId, innerCt);
                    if (uom is null)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_uom", "Target UoM does not exist in this tenant.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    var existing = await db.ItemUomConversions.SingleOrDefaultAsync(
                        c => c.TenantId == tenantId && c.ItemId == itemId && c.UomId == request.UomId,
                        innerCt);

                    try
                    {
                        if (existing is null)
                        {
                            existing = ItemUom.Create(
                                Guid.NewGuid(),
                                tenantId,
                                itemId,
                                request.UomId,
                                request.FactorToBase,
                                nowUtc);
                            db.ItemUomConversions.Add(existing);
                        }
                        else
                        {
                            existing.UpdateFactor(request.FactorToBase, request.Status, nowUtc);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_uom_conversion", ex.Message);
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "ITEM_UOM_CONVERSION_UPDATED",
                        nameof(ItemUom),
                        existing.ItemUomId.ToString("D"),
                        $"Set conversion 1 {uom.UomCode} = {existing.FactorToBase} for item {item.ItemCode}",
                        "catalog.item_uom.updated",
                        $"item-uom:{existing.ItemUomId:D}:v{existing.Version}",
                        new { itemId, uomId = uom.UomId, existing.FactorToBase, existing.Status },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildItemDetailAsync(db, tenantId, entityId, itemId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpsertItemUomConversion")
        .Produces<ItemDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPut("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items/{itemId:guid}/entity-policy", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            [FromBody] UpsertItemEntityPolicyRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMutationAsync(
                tenantId,
                entityId,
                PermissionCodes.CatalogManage,
                "duplicate_item_entity_policy",
                "Item entity policy conflict detected.",
                userManager,
                scopeExecutor,
                httpContext,
                async (scope, innerCt) =>
                {
                    var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, innerCt);
                    if (item is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "item_not_found", "Item was not found in this tenant.");
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    var existing = await db.ItemEntityPolicies.SingleOrDefaultAsync(
                        x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.ItemId == itemId,
                        innerCt);

                    try
                    {
                        if (existing is null)
                        {
                            existing = ItemEntityPolicy.Create(
                                Guid.NewGuid(),
                                tenantId,
                                entityId,
                                itemId,
                                request.PurchaseAllowed,
                                request.SalesAllowed,
                                request.Notes,
                                nowUtc);
                            db.ItemEntityPolicies.Add(existing);
                        }
                        else
                        {
                            existing.Update(request.PurchaseAllowed, request.SalesAllowed, request.Notes, nowUtc);
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        return EndpointSupport.BadRequest(httpContext, "invalid_item_policy", ex.Message);
                    }

                    EndpointSupport.RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        scope.ActorUserId,
                        "ITEM_ENTITY_POLICY_UPDATED",
                        nameof(ItemEntityPolicy),
                        existing.PolicyId.ToString("D"),
                        $"Updated {scope.EntityScope.Entity.Code} policy for item {item.ItemCode} (Purchase={existing.PurchaseAllowed}, Sales={existing.SalesAllowed})",
                        "catalog.item_policy.updated",
                        $"item-policy:{existing.PolicyId:D}:v{existing.Version}",
                        new { itemId, entityId, existing.PurchaseAllowed, existing.SalesAllowed },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    var dto = await BuildItemDetailAsync(db, tenantId, entityId, itemId, innerCt);
                    return Results.Ok(dto);
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("UpsertItemEntityPolicy")
        .Produces<ItemDetailDto>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/catalog/items/{itemId:guid}/convert-preview", async (
            Guid tenantId,
            Guid entityId,
            Guid itemId,
            [FromBody] ConvertQuantityPreviewRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            return await ExecuteMasterScopeAsync(
                tenantId,
                entityId,
                requireManage: false,
                [PermissionCodes.CatalogRead, PermissionCodes.CatalogManage],
                userManager,
                scopeExecutor,
                httpContext,
                async (_, innerCt) =>
                {
                    var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, innerCt);
                    if (item is null)
                    {
                        return EndpointSupport.NotFound(httpContext, "item_not_found", "Item was not found in this tenant.");
                    }

                    var baseUom = await db.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(u => u.TenantId == tenantId && u.UomId == item.BaseUomId, innerCt);
                    var sourceUom = await db.UnitsOfMeasure.AsNoTracking().SingleOrDefaultAsync(u => u.TenantId == tenantId && u.UomId == request.SourceUomId, innerCt);
                    if (baseUom is null || sourceUom is null)
                    {
                        return EndpointSupport.BadRequest(httpContext, "unsupported_uom_conversion", "Requested unit of measure was not found in this tenant.");
                    }

                    var conversion = request.SourceUomId == item.BaseUomId
                        ? null
                        : await db.ItemUomConversions.AsNoTracking().SingleOrDefaultAsync(
                            c => c.TenantId == tenantId && c.ItemId == itemId && c.UomId == request.SourceUomId,
                            innerCt);

                    try
                    {
                        var converted = UomConverter.ConvertToBase(item, baseUom, sourceUom, conversion, request.Quantity);
                        var roundTrip = sourceUom.QuantityPrecisionScale == 0 && converted.ConvertedBaseQuantity % converted.FactorToBase != 0m
                            ? decimal.Round(converted.ConvertedBaseQuantity / converted.FactorToBase, 6, MidpointRounding.AwayFromZero)
                            : UomConverter.ConvertFromBase(item, baseUom, sourceUom, conversion, converted.ConvertedBaseQuantity);

                        return Results.Ok(new ConvertQuantityPreviewResponse(
                            item.ItemId,
                            item.ItemCode,
                            converted.InputQuantity,
                            converted.InputUomCode,
                            converted.FactorToBase,
                            converted.ExactBaseQuantity,
                            converted.ConvertedBaseQuantity,
                            converted.RoundingResidual,
                            converted.BaseUomCode,
                            converted.BasePrecisionScale,
                            roundTrip,
                            DateTimeOffset.UtcNow));
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                    {
                        var code = ex.Message.StartsWith("fractional_discrete_base_quantity", StringComparison.Ordinal)
                            ? "fractional_discrete_base_quantity"
                            : ex.Message.StartsWith("unsupported_uom_conversion", StringComparison.Ordinal)
                                ? "unsupported_uom_conversion"
                                : "invalid_quantity";
                        return EndpointSupport.BadRequest(httpContext, code, ex.Message);
                    }
                },
                ct);
        })
        .RequireAuthorization()
        .WithName("ConvertItemQuantityPreview")
        .Produces<ConvertQuantityPreviewResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ExecuteMasterScopeAsync(
        Guid tenantId,
        Guid entityId,
        bool requireManage,
        string[] allowedPermissions,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        HttpContext httpContext,
        Func<AuthorizedEntityScope, CancellationToken, Task<IResult>> action,
        CancellationToken ct)
    {
        _ = requireManage;
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return EndpointSupport.Unauthenticated(httpContext);
        }

        var result = await scopeExecutor.ExecuteInEntityAsync(
            actorUserId,
            tenantId,
            entityId,
            requiredPermission: null,
            async (scope, innerCt) =>
            {
                if (!EndpointSupport.HasAnyPermission(scope.EffectivePermissions, allowedPermissions))
                {
                    return EndpointSupport.Forbidden(
                        httpContext,
                        "insufficient_entity_permission",
                        $"Requires {string.Join(" or ", allowedPermissions)} on this legal entity.");
                }

                return await action(scope, innerCt);
            },
            ct);

        return result.IsAllowed
            ? result.Value!
            : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
    }

    private static async Task<IResult> ExecuteMutationAsync(
        Guid tenantId,
        Guid entityId,
        string requiredPermission,
        string duplicateErrorCode,
        string duplicateMessage,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        HttpContext httpContext,
        Func<(AuthorizedEntityScope EntityScope, Guid ActorUserId), CancellationToken, Task<IResult>> action,
        CancellationToken ct)
    {
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!EndpointSupport.TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return EndpointSupport.Unauthenticated(httpContext);
        }

        try
        {
            var result = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission,
                async (scope, innerCt) => await action((scope, actorUserId), innerCt),
                ct);

            return result.IsAllowed
                ? result.Value!
                : EndpointSupport.MapScopeDenial(httpContext, result.DenialReason, result.ErrorCode);
        }
        catch (Exception ex) when (EndpointSupport.MapPersistenceConflict(httpContext, ex, duplicateErrorCode, duplicateMessage) is { } conflictResult)
        {
            return conflictResult;
        }
    }

    private static async Task<PartnerDetailDto?> BuildPartnerDetailAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid partnerId,
        CancellationToken ct)
    {
        var partner = await db.Partners.AsNoTracking().SingleOrDefaultAsync(p => p.TenantId == tenantId && p.PartnerId == partnerId, ct);
        if (partner is null)
        {
            return null;
        }

        var profile = await db.PartnerEntityProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.LegalEntityId == entityId && x.PartnerId == partnerId, ct);

        var contacts = await db.PartnerContacts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.PartnerId == partnerId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.ContactName)
            .Select(c => new PartnerContactDto(c.ContactId, c.ContactName, c.ContactRole, c.Email, c.Phone, c.IsPrimary, c.Status))
            .ToListAsync(ct);

        var (supEligible, cusEligible) = PartnerEligibilityEvaluator.Evaluate(partner, profile);
        var profileDto = profile is null
            ? null
            : new PartnerEntityProfileDto(
                profile.ProfileId,
                profile.LegalEntityId,
                profile.SupplierEligible,
                profile.CustomerEligible,
                profile.DefaultPaymentTermsDays,
                profile.Notes,
                profile.UpdatedAtUtc,
                profile.Version);

        return new PartnerDetailDto(
            partner.PartnerId,
            partner.PartnerCode,
            partner.LegalName,
            partner.DisplayName,
            partner.IsSupplier,
            partner.IsCustomer,
            partner.TaxRegistrationNpwp,
            partner.TaxPkpStatus,
            partner.EntityScopeMode,
            partner.Status,
            supEligible,
            cusEligible,
            profileDto,
            contacts,
            partner.CreatedAtUtc,
            partner.UpdatedAtUtc,
            partner.DeactivatedAtUtc,
            partner.Version);
    }

    private static async Task<ItemDetailDto?> BuildItemDetailAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid itemId,
        CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(i => i.TenantId == tenantId && i.ItemId == itemId, ct);
        if (item is null)
        {
            return null;
        }

        var uoms = await db.UnitsOfMeasure.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .ToDictionaryAsync(u => u.UomId, ct);

        var conversions = await (
            from c in db.ItemUomConversions.AsNoTracking()
            join u in db.UnitsOfMeasure.AsNoTracking() on new { c.TenantId, c.UomId } equals new { u.TenantId, u.UomId }
            where c.TenantId == tenantId && c.ItemId == itemId
            orderby u.UomCode
            select new ItemUomDto(
                c.ItemUomId,
                c.UomId,
                u.UomCode,
                u.Name,
                u.QuantityPrecisionScale,
                c.FactorToBase,
                c.Status,
                c.UpdatedAtUtc,
                c.Version))
            .ToListAsync(ct);

        var policy = await db.ItemEntityPolicies.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.ItemId == itemId, ct);

        var (purchaseAllowed, salesAllowed) = ItemEligibilityEvaluator.Evaluate(item, policy);
        var baseUom = uoms[item.BaseUomId];
        var purchUom = uoms[item.DefaultPurchaseUomId];
        var salesUom = uoms[item.DefaultSalesUomId];

        var policyDto = policy is null
            ? null
            : new ItemEntityPolicyDto(
                policy.PolicyId,
                policy.LegalEntityId,
                policy.PurchaseAllowed,
                policy.SalesAllowed,
                policy.Notes,
                policy.UpdatedAtUtc,
                policy.Version);

        return new ItemDetailDto(
            item.ItemId,
            item.ItemCode,
            item.Name,
            item.ItemKind,
            item.LotTrackingPolicy,
            item.BaseUomId,
            baseUom.UomCode,
            baseUom.QuantityPrecisionScale,
            item.DefaultPurchaseUomId,
            purchUom.UomCode,
            item.DefaultSalesUomId,
            salesUom.UomCode,
            item.EntityScopeMode,
            item.Status,
            purchaseAllowed,
            salesAllowed,
            policyDto,
            conversions,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.DeactivatedAtUtc,
            item.Version);
    }
}
