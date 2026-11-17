using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Api.Endpoints;

public static class AdminAndApprovalEndpoints
{
    public static IEndpointRouteBuilder MapAdminAndApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/overview", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission: null,
                async (scope, innerCt) =>
                {
                    var hasAdmin = scope.EffectivePermissions.Contains(PermissionCodes.AdminRead, StringComparer.Ordinal)
                        || scope.EffectivePermissions.Contains(PermissionCodes.AdminManage, StringComparer.Ordinal);
                    if (!hasAdmin)
                    {
                        return (Error: Forbidden(httpContext, "insufficient_entity_permission", "Requires admin.read or admin.manage on this legal entity."), Data: (AdminOverviewResponse?)null);
                    }

                    var members = await BuildEntityMembersAsync(db, tenantId, entityId, innerCt);
                    var invitations = await db.UserInvitations
                        .AsNoTracking()
                        .Where(i => i.TenantId == tenantId && i.LegalEntityId == entityId)
                        .OrderByDescending(i => i.CreatedAtUtc)
                        .Select(i => new InvitationDto(
                            i.InvitationId,
                            i.InvitedEmail,
                            i.RoleTemplateCode,
                            i.Status,
                            i.ExpiresAtUtc,
                            i.CreatedAtUtc,
                            i.AcceptedAtUtc,
                            i.RevokedAtUtc))
                        .ToListAsync(innerCt);

                    var policies = await BuildPoliciesAsync(db, tenantId, entityId, innerCt);

                    var auditEvents = await (
                        from evt in db.AuditEvents.AsNoTracking()
                        join usr in db.Users.AsNoTracking() on evt.ActorUserId equals usr.Id
                        where evt.TenantId == tenantId && evt.LegalEntityId == entityId
                        orderby evt.OccurredAtUtc descending
                        select new AuditEventDto(
                            evt.EventId,
                            evt.ActorUserId,
                            usr.Email ?? evt.ActorUserId.ToString("D"),
                            evt.EventType,
                            evt.SubjectType,
                            evt.SubjectId,
                            evt.Summary,
                            evt.OccurredAtUtc))
                        .Take(50)
                        .ToListAsync(innerCt);

                    var outboxSummary = await BuildOutboxSummaryAsync(db, tenantId, innerCt);

                    return (Error: (IResult?)null, Data: new AdminOverviewResponse(
                        members,
                        invitations,
                        policies,
                        auditEvents,
                        outboxSummary,
                        PermissionCodes.KnownAll,
                        RoleTemplates.AllCodes));
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            return scopeResult.Value.Error ?? Results.Ok(scopeResult.Value.Data);
        })
        .RequireAuthorization()
        .WithName("GetEntityAdminOverview")
        .Produces<AdminOverviewResponse>(StatusCodes.Status200OK);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/invitations", async (
            Guid tenantId,
            Guid entityId,
            CreateInvitationRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Trim().Length > 256)
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_email", "Invalid email address", "Provide a valid recipient work email.");
            }

            if (!RoleTemplates.IsKnown(request.RoleTemplateCode))
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_role_template", "Invalid role template", "Select an approved entity role template.");
            }

            var expiryHours = Math.Clamp(request.ExpiresInHours <= 0 ? 48 : request.ExpiresInHours, 1, 168);

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                PermissionCodes.AdminManage,
                async (_, innerCt) =>
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    var (invitation, rawToken) = UserInvitation.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        request.Email,
                        request.RoleTemplateCode,
                        actorUserId,
                        nowUtc.AddHours(expiryHours),
                        nowUtc);

                    db.UserInvitations.Add(invitation);
                    RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        "INVITATION_CREATED",
                        "UserInvitation",
                        invitation.InvitationId.ToString("D"),
                        $"Invited {invitation.InvitedEmail} as {invitation.RoleTemplateCode}",
                        "identity.invitation.created.v1",
                        $"inv-created:{invitation.InvitationId:N}",
                        new { invitation.InvitationId, invitation.InvitedEmail, invitation.RoleTemplateCode },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);

                    return new CreateInvitationResponse(
                        invitation.InvitationId,
                        invitation.InvitedEmail,
                        invitation.RoleTemplateCode,
                        invitation.Status,
                        invitation.ExpiresAtUtc,
                        rawToken);
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            outboxSignal.NotifyTenant(tenantId);
            return Results.Created(
                $"/api/workspaces/{tenantId}/entities/{entityId}/admin/overview",
                scopeResult.Value);
        })
        .RequireAuthorization()
        .WithName("CreateInvitation")
        .Produces<CreateInvitationResponse>(StatusCodes.Status201Created);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/invitations/{invitationId:guid}/revoke", async (
            Guid tenantId,
            Guid entityId,
            Guid invitationId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                PermissionCodes.AdminManage,
                async (_, innerCt) =>
                {
                    var invitation = await db.UserInvitations
                        .SingleOrDefaultAsync(i => i.TenantId == tenantId && i.LegalEntityId == entityId && i.InvitationId == invitationId, innerCt);
                    if (invitation is null)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "invitation_not_found", "Invitation not found", "Invitation was not found for this legal entity."), Status: (string?)null);
                    }

                    if (invitation.Status != InvitationStatuses.Pending)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "invitation_not_pending", "Invitation cannot be revoked", $"Invitation is already {invitation.Status}."), Status: (string?)null);
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    invitation.Revoke(nowUtc);
                    RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        "INVITATION_REVOKED",
                        "UserInvitation",
                        invitation.InvitationId.ToString("D"),
                        $"Revoked pending invitation for {invitation.InvitedEmail}",
                        "identity.invitation.revoked.v1",
                        $"inv-revoked:{invitation.InvitationId:N}",
                        new { invitation.InvitationId, invitation.InvitedEmail },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    return (Error: (IResult?)null, Status: (string?)invitation.Status);
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            if (scopeResult.Value.Error is not null)
            {
                return scopeResult.Value.Error;
            }

            outboxSignal.NotifyTenant(tenantId);
            return Results.Ok(new { invitationId, status = scopeResult.Value.Status });
        })
        .RequireAuthorization()
        .WithName("RevokeInvitation");

        app.MapPost("/api/invitations/accept", async (
            AcceptInvitationRequest request,
            UserManager<ApplicationUser> userManager,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invitation_token_required", "Invitation token required", "Provide a valid invitation token.");
            }

            var actorUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actorUserId, ct);
            var accountActive = await db.UserAccounts.AsNoTracking().AnyAsync(u => u.UserId == actorUserId && u.IsActive, ct);
            if (actorUser is null || !accountActive)
            {
                return Unauthenticated(httpContext);
            }

            var tokenHash = UserInvitation.ComputeTokenHash(request.Token);
            var nowUtc = DateTimeOffset.UtcNow;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.actor_user_id', {actorUserId.ToString("D")}, true), set_config('app.tenant_id', '', true), set_config('app.invitation_token_hash', {tokenHash}, true)",
                ct);

            var invitation = await db.UserInvitations.SingleOrDefaultAsync(i => i.TokenHash == tokenHash, ct);
            if (invitation is null)
            {
                await tx.RollbackAsync(ct);
                return ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "invitation_not_found", "Invitation not found", "The invitation token is invalid or unrecognized.");
            }

            if (invitation.Status == InvitationStatuses.Revoked)
            {
                await tx.RollbackAsync(ct);
                return ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "invitation_revoked", "Invitation revoked", "This invitation was revoked by an administrator.");
            }

            if (invitation.Status == InvitationStatuses.Accepted)
            {
                await tx.RollbackAsync(ct);
                return ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "invitation_already_accepted", "Invitation already used", "This invitation has already been accepted.");
            }

            if (invitation.IsExpiredAt(nowUtc))
            {
                await tx.RollbackAsync(ct);
                return ProblemResults.Create(httpContext, StatusCodes.Status410Gone, "invitation_expired", "Invitation expired", "This invitation has expired.");
            }

            var actorEmail = actorUser.Email ?? actorUser.UserName ?? string.Empty;
            if (!string.Equals(actorEmail.Trim(), invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
            {
                await tx.RollbackAsync(ct);
                return Forbidden(httpContext, "invitation_email_mismatch", "Your signed-in email does not match the invited recipient.");
            }

            // Switch transaction-local app.tenant_id AFTER verified invitation resolution
            var tidStr = invitation.TenantId.ToString("D");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {tidStr}, true)",
                ct);

            var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.TenantId == invitation.TenantId && t.Status == TenantStatuses.Active, ct);
            var entity = await db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(
                e => e.TenantId == invitation.TenantId && e.LegalEntityId == invitation.LegalEntityId && e.Status == LegalEntityStatuses.Active,
                ct);

            if (tenant is null || entity is null)
            {
                await tx.RollbackAsync(ct);
                return ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "workspace_not_found", "Workspace inactive", "Target tenant or legal entity is no longer active.");
            }

            var membership = await db.TenantMemberships.SingleOrDefaultAsync(
                m => m.TenantId == invitation.TenantId && m.UserId == actorUserId,
                ct);
            if (membership is null)
            {
                membership = TenantMembership.Create(Guid.NewGuid(), invitation.TenantId, actorUserId, nowUtc);
                db.TenantMemberships.Add(membership);
                await db.SaveChangesAsync(ct);
            }
            else if (!membership.IsActive)
            {
                membership.Reactivate(nowUtc);
                await db.SaveChangesAsync(ct);
            }

            // Grant ONLY invited entity role template permissions; never reactivate prior REVOKED grants elsewhere
            var templatePermissions = RoleTemplates.GetPermissions(invitation.RoleTemplateCode);
            var existingEntityGrants = await db.EntityAccessGrants
                .Where(g => g.TenantId == invitation.TenantId
                    && g.LegalEntityId == invitation.LegalEntityId
                    && g.UserId == actorUserId
                    && g.Status != GrantStatuses.Revoked)
                .ToListAsync(ct);

            foreach (var perm in templatePermissions)
            {
                if (existingEntityGrants.All(g => !string.Equals(g.PermissionCode, perm, StringComparison.Ordinal)))
                {
                    db.EntityAccessGrants.Add(EntityAccessGrant.Create(
                        Guid.NewGuid(),
                        invitation.TenantId,
                        invitation.LegalEntityId,
                        actorUserId,
                        perm,
                        nowUtc.AddMinutes(-1),
                        null,
                        nowUtc));
                }
            }

            invitation.Accept(actorUserId, nowUtc);
            RecordAuditAndOutbox(
                db,
                invitation.TenantId,
                invitation.LegalEntityId,
                actorUserId,
                "INVITATION_ACCEPTED",
                "UserInvitation",
                invitation.InvitationId.ToString("D"),
                $"User {actorEmail} accepted invitation for {entity.Code} ({invitation.RoleTemplateCode})",
                "identity.invitation.accepted.v1",
                $"inv-accept:{invitation.InvitationId:N}",
                new { invitation.InvitationId, actorUserId, invitation.RoleTemplateCode },
                nowUtc);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            outboxSignal.NotifyTenant(invitation.TenantId);

            return Results.Ok(new AcceptInvitationResponse(
                tenant.TenantId,
                entity.LegalEntityId,
                tenant.Code,
                entity.Code,
                templatePermissions));
        })
        .RequireAuthorization()
        .WithName("AcceptInvitation")
        .Produces<AcceptInvitationResponse>(StatusCodes.Status200OK);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/grants", async (
            Guid tenantId,
            Guid entityId,
            GrantPermissionRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            if (!PermissionCodes.IsKnown(request.PermissionCode))
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_permission_code", "Invalid permission code", "Permission code is not in the approved platform catalog.");
            }

            var normalizedPerm = PermissionCodes.NormalizeKnown(request.PermissionCode);

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                PermissionCodes.AdminManage,
                async (_, innerCt) =>
                {
                    var membership = await db.TenantMemberships.AsNoTracking()
                        .SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == request.TargetUserId && m.Status == MembershipStatuses.Active, innerCt);
                    if (membership is null)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "target_member_not_found", "Member not active", "Target user must hold an active membership in this tenant."), Granted: (string?)null);
                    }

                    var exists = await db.EntityAccessGrants.AnyAsync(
                        g => g.TenantId == tenantId
                            && g.LegalEntityId == entityId
                            && g.UserId == request.TargetUserId
                            && g.PermissionCode == normalizedPerm
                            && g.Status != GrantStatuses.Revoked,
                        innerCt);
                    if (exists)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "grant_already_exists", "Permission already granted", $"Member already holds {normalizedPerm} on this entity."), Granted: (string?)null);
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    db.EntityAccessGrants.Add(EntityAccessGrant.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        request.TargetUserId,
                        normalizedPerm,
                        nowUtc.AddMinutes(-1),
                        null,
                        nowUtc));

                    RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        "ENTITY_GRANT_ADDED",
                        "EntityAccessGrant",
                        $"{request.TargetUserId:D}:{normalizedPerm}",
                        $"Granted {normalizedPerm} to user {request.TargetUserId:D}",
                        "identity.grant.added.v1",
                        $"grant:{entityId:N}:{request.TargetUserId:N}:{normalizedPerm}:{nowUtc.Ticks}",
                        new { request.TargetUserId, PermissionCode = normalizedPerm },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);
                    return (Error: (IResult?)null, Granted: (string?)normalizedPerm);
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            if (scopeResult.Value.Error is not null)
            {
                return scopeResult.Value.Error;
            }

            outboxSignal.NotifyTenant(tenantId);
            return Results.Ok(new { targetUserId = request.TargetUserId, permissionCode = scopeResult.Value.Granted });
        })
        .RequireAuthorization()
        .WithName("GrantEntityPermission");

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/members/{targetUserId:guid}/suspend", async (
            Guid tenantId,
            Guid entityId,
            Guid targetUserId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            await TransitionMembershipStatusAsync(
                tenantId, entityId, targetUserId, revokeAllGrants: false,
                userManager, scopeExecutor, outboxSignal, db, httpContext, ct))
        .RequireAuthorization()
        .WithName("SuspendTenantMember");

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/members/{targetUserId:guid}/revoke", async (
            Guid tenantId,
            Guid entityId,
            Guid targetUserId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
            await TransitionMembershipStatusAsync(
                tenantId, entityId, targetUserId, revokeAllGrants: true,
                userManager, scopeExecutor, outboxSignal, db, httpContext, ct))
        .RequireAuthorization()
        .WithName("RevokeTenantMember");

        app.MapGet("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/approvals/cases", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                requiredPermission: null,
                async (_, innerCt) =>
                {
                    var cases = await QueryApprovalCasesAsync(db, tenantId, entityId, caseIdFilter: null, innerCt);
                    var policies = await BuildPoliciesAsync(db, tenantId, entityId, innerCt);
                    return new ApprovalCasesResponse(cases, policies);
                },
                ct);

            return !scopeResult.IsAllowed
                ? MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode)
                : Results.Ok(scopeResult.Value);
        })
        .RequireAuthorization()
        .WithName("ListApprovalCases")
        .Produces<ApprovalCasesResponse>(StatusCodes.Status200OK);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/approvals/cases", async (
            Guid tenantId,
            Guid entityId,
            CreateApprovalCaseRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            if (!PermissionCodes.IsKnown(request.TargetPermissionCode))
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_permission_code", "Invalid permission code", "Requested permission code is invalid.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_reason", "Reason required", "Provide a justification between 1 and 500 characters.");
            }

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
            if (idempotencyKey is { Length: > 128 })
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_idempotency_key", "Idempotency-Key too long", "Idempotency-Key must not exceed 128 characters.");
            }

            var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                PermissionCodes.ApprovalRequest,
                async (_, innerCt) =>
                {
                    if (!string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        var existingKey = await db.CommandIdempotencies.AsNoTracking().SingleOrDefaultAsync(
                            k => k.TenantId == tenantId
                                && k.LegalEntityId == entityId
                                && k.OperationCode == "APPROVAL_REQUEST"
                                && k.IdempotencyKey == idempotencyKey,
                            innerCt);
                        if (existingKey is not null && Guid.TryParse(existingKey.ResourceId, out var existingCaseId))
                        {
                            var replay = (await QueryApprovalCasesAsync(db, tenantId, entityId, existingCaseId, innerCt)).Single();
                            return (Error: (IResult?)null, Replayed: true, Case: (ApprovalCaseDto?)replay);
                        }
                    }

                    var policyCode = request.PolicyCode.Trim().ToUpperInvariant();
                    var policy = await db.ApprovalPolicies.AsNoTracking().SingleOrDefaultAsync(
                        p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyCode == policyCode && p.IsActive,
                        innerCt);
                    if (policy is null)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "approval_policy_not_found", "Policy not found", $"Approval policy '{policyCode}' is not active on this entity."), Replayed: false, Case: (ApprovalCaseDto?)null);
                    }

                    var targetUserId = request.TargetUserId is { } tid && tid != Guid.Empty ? tid : actorUserId;
                    var targetMemberActive = await db.TenantMemberships.AsNoTracking().AnyAsync(
                        m => m.TenantId == tenantId && m.UserId == targetUserId && m.Status == MembershipStatuses.Active,
                        innerCt);
                    if (!targetMemberActive)
                    {
                        return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "target_member_not_found", "Target member inactive", "Target user is not an active member of this tenant."), Replayed: false, Case: (ApprovalCaseDto?)null);
                    }

                    var nowUtc = DateTimeOffset.UtcNow;
                    var approvalCase = ApprovalCase.Create(
                        Guid.NewGuid(),
                        tenantId,
                        entityId,
                        policy.PolicyId,
                        ApprovalActionTypes.EntityAccessGrantRequest,
                        targetUserId,
                        request.TargetPermissionCode,
                        request.Reason,
                        actorUserId,
                        nowUtc);

                    db.ApprovalCases.Add(approvalCase);

                    if (!string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        db.CommandIdempotencies.Add(CommandIdempotency.Create(
                            Guid.NewGuid(),
                            tenantId,
                            entityId,
                            "APPROVAL_REQUEST",
                            idempotencyKey,
                            actorUserId,
                            approvalCase.CaseId.ToString("D"),
                            StatusCodes.Status201Created,
                            nowUtc));
                    }

                    RecordAuditAndOutbox(
                        db,
                        tenantId,
                        entityId,
                        actorUserId,
                        "APPROVAL_REQUESTED",
                        "ApprovalCase",
                        approvalCase.CaseId.ToString("D"),
                        $"Requested {approvalCase.TargetPermissionCode} under policy {policy.PolicyCode}",
                        "approval.case.requested.v1",
                        $"case-req:{approvalCase.CaseId:N}",
                        new { approvalCase.CaseId, approvalCase.TargetUserId, approvalCase.TargetPermissionCode, policy.PolicyCode },
                        nowUtc);

                    await db.SaveChangesAsync(innerCt);

                    var dto = (await QueryApprovalCasesAsync(db, tenantId, entityId, approvalCase.CaseId, innerCt)).Single();
                    return (Error: (IResult?)null, Replayed: false, Case: (ApprovalCaseDto?)dto);
                },
                ct);

            if (!scopeResult.IsAllowed)
            {
                return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
            }

            if (scopeResult.Value.Error is not null)
            {
                return scopeResult.Value.Error;
            }

            if (!scopeResult.Value.Replayed)
            {
                outboxSignal.NotifyTenant(tenantId);
            }

            return scopeResult.Value.Replayed
                ? Results.Ok(scopeResult.Value.Case)
                : Results.Created($"/api/workspaces/{tenantId}/entities/{entityId}/approvals/cases", scopeResult.Value.Case);
        })
        .RequireAuthorization()
        .WithName("CreateApprovalCase")
        .Produces<ApprovalCaseDto>(StatusCodes.Status201Created);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/approvals/cases/{caseId:guid}/decide", async (
            Guid tenantId,
            Guid entityId,
            Guid caseId,
            DecideApprovalCaseRequest request,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            ITenantOutboxSignal outboxSignal,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            var normalizedDecision = request.Decision?.Trim().ToUpperInvariant() ?? string.Empty;
            if (normalizedDecision != ApprovalDecisions.Approved && normalizedDecision != ApprovalDecisions.Rejected)
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "invalid_decision", "Invalid decision", "Decision must be APPROVED or REJECTED.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "decision_reason_required", "Decision reason required", "Provide a justification between 1 and 500 characters.");
            }

            try
            {
                var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
                    actorUserId,
                    tenantId,
                    entityId,
                    PermissionCodes.ApprovalDecide,
                    async (_, innerCt) =>
                    {
                        var approvalCase = await db.ApprovalCases.SingleOrDefaultAsync(
                            c => c.TenantId == tenantId && c.LegalEntityId == entityId && c.CaseId == caseId,
                            innerCt);
                        if (approvalCase is null)
                        {
                            return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "approval_case_not_found", "Approval case not found", "The requested approval case does not exist in this entity."), Case: (ApprovalCaseDto?)null);
                        }

                        if (approvalCase.RequestedByUserId == actorUserId)
                        {
                            return (Error: Forbidden(httpContext, "four_eyes_self_approval_forbidden", "Four-eyes policy (OD-02) prohibits approving or deciding your own request."), Case: (ApprovalCaseDto?)null);
                        }

                        if (approvalCase.Status != ApprovalCaseStatuses.Pending)
                        {
                            return (Error: ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "case_already_decided", "Case already decided", $"This approval case is already {approvalCase.Status}."), Case: (ApprovalCaseDto?)null);
                        }

                        var policy = await db.ApprovalPolicies.AsNoTracking().SingleAsync(
                            p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.PolicyId == approvalCase.PolicyId,
                            innerCt);

                        var actorIdentity = await db.Users.AsNoTracking().SingleAsync(u => u.Id == actorUserId, innerCt);
                        if (policy.RequiresApproverMfa && !actorIdentity.TwoFactorEnabled)
                        {
                            return (Error: Forbidden(httpContext, "mfa_required", "This approval policy requires TOTP MFA enrollment before deciding."), Case: (ApprovalCaseDto?)null);
                        }

                        var nowUtc = DateTimeOffset.UtcNow;
                        approvalCase.RecordDecision(actorUserId, normalizedDecision, request.Reason, nowUtc);

                        db.ApprovalDecisions.Add(ApprovalDecision.Create(
                            Guid.NewGuid(),
                            tenantId,
                            entityId,
                            approvalCase.CaseId,
                            approvalCase.RequestedByUserId,
                            actorUserId,
                            normalizedDecision,
                            request.Reason,
                            actorIdentity.TwoFactorEnabled,
                            nowUtc));

                        if (normalizedDecision == ApprovalDecisions.Approved && approvalCase.ActionType == ApprovalActionTypes.EntityAccessGrantRequest)
                        {
                            var targetMemberActive = await db.TenantMemberships.AsNoTracking().AnyAsync(
                                m => m.TenantId == tenantId && m.UserId == approvalCase.TargetUserId && m.Status == MembershipStatuses.Active,
                                innerCt);
                            if (!targetMemberActive)
                            {
                                return (Error: ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "target_member_inactive", "Target user inactive", "Target tenant membership is no longer active."), Case: (ApprovalCaseDto?)null);
                            }

                            var hasGrant = await db.EntityAccessGrants.AnyAsync(
                                g => g.TenantId == tenantId
                                    && g.LegalEntityId == entityId
                                    && g.UserId == approvalCase.TargetUserId
                                    && g.PermissionCode == approvalCase.TargetPermissionCode
                                    && g.Status != GrantStatuses.Revoked,
                                innerCt);
                            if (!hasGrant)
                            {
                                db.EntityAccessGrants.Add(EntityAccessGrant.Create(
                                    Guid.NewGuid(),
                                    tenantId,
                                    entityId,
                                    approvalCase.TargetUserId,
                                    approvalCase.TargetPermissionCode,
                                    nowUtc.AddMinutes(-1),
                                    null,
                                    nowUtc));
                            }
                        }

                        RecordAuditAndOutbox(
                            db,
                            tenantId,
                            entityId,
                            actorUserId,
                            $"APPROVAL_{normalizedDecision}",
                            "ApprovalCase",
                            approvalCase.CaseId.ToString("D"),
                            $"{normalizedDecision} {approvalCase.TargetPermissionCode} request for {approvalCase.TargetUserId:D}",
                            "approval.case.decided.v1",
                            $"case-decide:{approvalCase.CaseId:N}",
                            new { approvalCase.CaseId, normalizedDecision, approvalCase.TargetUserId, approvalCase.TargetPermissionCode, actorIdentity.TwoFactorEnabled },
                            nowUtc);

                        await db.SaveChangesAsync(innerCt);

                        var dto = (await QueryApprovalCasesAsync(db, tenantId, entityId, approvalCase.CaseId, innerCt)).Single();
                        return (Error: (IResult?)null, Case: (ApprovalCaseDto?)dto);
                    },
                    ct);

                if (!scopeResult.IsAllowed)
                {
                    return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
                }

                if (scopeResult.Value.Error is not null)
                {
                    return scopeResult.Value.Error;
                }

                outboxSignal.NotifyTenant(tenantId);
                return Results.Ok(scopeResult.Value.Case);
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException
                || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
            {
                return ProblemResults.Create(httpContext, StatusCodes.Status409Conflict, "case_already_decided", "Concurrent decision conflict", "Another approver decided this case simultaneously.");
            }
        })
        .RequireAuthorization()
        .WithName("DecideApprovalCase")
        .Produces<ApprovalCaseDto>(StatusCodes.Status200OK);

        app.MapPost("/api/workspaces/{tenantId:guid}/entities/{entityId:guid}/admin/outbox/dispatch", async (
            Guid tenantId,
            Guid entityId,
            UserManager<ApplicationUser> userManager,
            TenantScopeExecutor scopeExecutor,
            TenantOutboxProcessor outboxProcessor,
            AtheraDbContext db,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
            if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
            {
                return Unauthenticated(httpContext);
            }

            var authCheck = await scopeExecutor.ExecuteInEntityAsync(
                actorUserId,
                tenantId,
                entityId,
                PermissionCodes.AdminManage,
                static (_, _) => Task.FromResult(true),
                ct);

            if (!authCheck.IsAllowed)
            {
                return MapScopeDenial(httpContext, authCheck.DenialReason, authCheck.ErrorCode);
            }

            var result = await outboxProcessor.ProcessPendingForTenantAsync(tenantId, null, ct);

            var summaryCheck = await scopeExecutor.ExecuteInTenantAsync(
                actorUserId,
                tenantId,
                (_, innerCt) => BuildOutboxSummaryAsync(db, tenantId, innerCt),
                ct);

            return Results.Ok(new OutboxDispatchResponse(
                result.ProcessedCount,
                result.FailedCount,
                summaryCheck.Value ?? new OutboxSummaryDto(0, 0, 0)));
        })
        .RequireAuthorization()
        .WithName("DispatchEntityOutbox")
        .Produces<OutboxDispatchResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> TransitionMembershipStatusAsync(
        Guid tenantId,
        Guid entityId,
        Guid targetUserId,
        bool revokeAllGrants,
        UserManager<ApplicationUser> userManager,
        TenantScopeExecutor scopeExecutor,
        ITenantOutboxSignal outboxSignal,
        AtheraDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        HealthAndAuthEndpoints.SetNoStoreHeaders(httpContext);
        if (!TryGetActorUserId(userManager, httpContext, out var actorUserId))
        {
            return Unauthenticated(httpContext);
        }

        if (targetUserId == actorUserId)
        {
            return ProblemResults.Create(httpContext, StatusCodes.Status400BadRequest, "cannot_modify_own_membership", "Self-lockout blocked", "Administrators cannot suspend or revoke their own tenant membership.");
        }

        var scopeResult = await scopeExecutor.ExecuteInEntityAsync(
            actorUserId,
            tenantId,
            entityId,
            PermissionCodes.AdminManage,
            async (_, innerCt) =>
            {
                var membership = await db.TenantMemberships.SingleOrDefaultAsync(
                    m => m.TenantId == tenantId && m.UserId == targetUserId,
                    innerCt);
                if (membership is null)
                {
                    return (Error: ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, "member_not_found", "Member not found", "Target membership was not found in this tenant."), Payload: (MemberStatusResponse?)null);
                }

                var nowUtc = DateTimeOffset.UtcNow;
                var affectedGrants = 0;

                if (revokeAllGrants)
                {
                    membership.Revoke(nowUtc);
                    var activeGrants = await db.EntityAccessGrants
                        .Where(g => g.TenantId == tenantId && g.UserId == targetUserId && g.Status != GrantStatuses.Revoked)
                        .ToListAsync(innerCt);
                    foreach (var grant in activeGrants)
                    {
                        grant.Revoke(nowUtc);
                        affectedGrants++;
                    }
                }
                else
                {
                    membership.Suspend(nowUtc);
                }

                RecordAuditAndOutbox(
                    db,
                    tenantId,
                    entityId,
                    actorUserId,
                    revokeAllGrants ? "MEMBERSHIP_REVOKED" : "MEMBERSHIP_SUSPENDED",
                    "TenantMembership",
                    targetUserId.ToString("D"),
                    $"{(revokeAllGrants ? "Revoked" : "Suspended")} membership for {targetUserId:D}",
                    revokeAllGrants ? "identity.membership.revoked.v1" : "identity.membership.suspended.v1",
                    $"member-{(revokeAllGrants ? "rev" : "susp")}:{targetUserId:N}:{nowUtc.Ticks}",
                    new { targetUserId, membership.Status, affectedGrants },
                    nowUtc);

                await db.SaveChangesAsync(innerCt);
                return (Error: (IResult?)null, Payload: (MemberStatusResponse?)new MemberStatusResponse(targetUserId, membership.Status, affectedGrants));
            },
            ct);

        if (!scopeResult.IsAllowed)
        {
            return MapScopeDenial(httpContext, scopeResult.DenialReason, scopeResult.ErrorCode);
        }

        if (scopeResult.Value.Error is not null)
        {
            return scopeResult.Value.Error;
        }

        outboxSignal.NotifyTenant(tenantId);
        return Results.Ok(scopeResult.Value.Payload);
    }

    private static async Task<List<EntityMemberDto>> BuildEntityMembersAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        var members = await (
            from m in db.TenantMemberships.AsNoTracking()
            join acct in db.UserAccounts.AsNoTracking() on m.UserId equals acct.UserId
            join usr in db.Users.AsNoTracking() on m.UserId equals usr.Id
            where m.TenantId == tenantId
            orderby acct.DisplayName
            select new
            {
                m.UserId,
                Email = usr.Email ?? string.Empty,
                acct.DisplayName,
                m.Status,
                usr.TwoFactorEnabled
            })
            .ToListAsync(ct);

        var grants = await db.EntityAccessGrants.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.LegalEntityId == entityId && g.Status == GrantStatuses.Active)
            .Select(g => new { g.UserId, g.PermissionCode })
            .ToListAsync(ct);

        var grantLookup = grants
            .GroupBy(g => g.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.PermissionCode).Distinct().OrderBy(p => p).ToList());

        return members
            .Select(m => new EntityMemberDto(
                m.UserId,
                m.Email,
                m.DisplayName,
                m.Status,
                m.TwoFactorEnabled,
                grantLookup.TryGetValue(m.UserId, out var list) ? list : Array.Empty<string>()))
            .ToList();
    }

    private static async Task<List<ApprovalPolicyDto>> BuildPoliciesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        CancellationToken ct)
    {
        return await db.ApprovalPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.LegalEntityId == entityId && p.IsActive)
            .OrderBy(p => p.PolicyCode)
            .Select(p => new ApprovalPolicyDto(p.PolicyId, p.PolicyCode, p.Name, p.RequiresFourEyes, p.RequiresApproverMfa, p.IsActive))
            .ToListAsync(ct);
    }

    private static async Task<List<ApprovalCaseDto>> QueryApprovalCasesAsync(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid? caseIdFilter,
        CancellationToken ct)
    {
        var query = from c in db.ApprovalCases.AsNoTracking()
                    join p in db.ApprovalPolicies.AsNoTracking()
                        on new { c.TenantId, c.LegalEntityId, c.PolicyId } equals new { p.TenantId, p.LegalEntityId, p.PolicyId }
                    join reqUser in db.UserAccounts.AsNoTracking()
                        on c.RequestedByUserId equals reqUser.UserId
                    join dec in db.ApprovalDecisions.AsNoTracking()
                        on new { c.TenantId, c.LegalEntityId, c.CaseId } equals new { dec.TenantId, dec.LegalEntityId, dec.CaseId }
                        into decJoin
                    from dec in decJoin.DefaultIfEmpty()
                    join decUser in db.UserAccounts.AsNoTracking()
                        on c.DecidedByUserId equals decUser.UserId
                        into decUserJoin
                    from decUser in decUserJoin.DefaultIfEmpty()
                    where c.TenantId == tenantId && c.LegalEntityId == entityId
                    select new { c, p, reqUser, dec, decUser };

        if (caseIdFilter.HasValue)
        {
            var cid = caseIdFilter.Value;
            query = query.Where(x => x.c.CaseId == cid);
        }

        return await query
            .OrderByDescending(x => x.c.RequestedAtUtc)
            .Select(x => new ApprovalCaseDto(
                x.c.CaseId,
                x.c.TenantId,
                x.c.LegalEntityId,
                x.c.PolicyId,
                x.p.PolicyCode,
                x.p.RequiresApproverMfa,
                x.c.ActionType,
                x.c.TargetUserId,
                x.c.TargetPermissionCode,
                x.c.RequestReason,
                x.c.Status,
                x.c.RequestedByUserId,
                x.reqUser.DisplayName,
                x.c.RequestedAtUtc,
                x.c.DecidedByUserId,
                x.decUser != null ? x.decUser.DisplayName : null,
                x.c.DecidedAtUtc,
                x.dec != null ? x.dec.Reason : null,
                x.dec != null ? x.dec.ApproverMfaVerified : null))
            .ToListAsync(ct);
    }

    private static async Task<OutboxSummaryDto> BuildOutboxSummaryAsync(
        AtheraDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var counts = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new OutboxSummaryDto(
            counts.Where(x => x.Status == OutboxStatuses.Pending).Sum(x => x.Count),
            counts.Where(x => x.Status == OutboxStatuses.Processed).Sum(x => x.Count),
            counts.Where(x => x.Status == OutboxStatuses.Failed).Sum(x => x.Count));
    }

    private static void RecordAuditAndOutbox(
        AtheraDbContext db,
        Guid tenantId,
        Guid entityId,
        Guid actorUserId,
        string eventType,
        string subjectType,
        string subjectId,
        string summary,
        string outboxTopic,
        string sourceKey,
        object payload,
        DateTimeOffset nowUtc)
    {
        var json = JsonSerializer.Serialize(payload);
        db.AuditEvents.Add(AuditEvent.Append(
            Guid.NewGuid(),
            tenantId,
            entityId,
            actorUserId,
            eventType,
            subjectType,
            subjectId,
            summary,
            json,
            nowUtc));

        db.OutboxMessages.Add(OutboxMessage.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            outboxTopic,
            sourceKey,
            json,
            nowUtc));
    }

    private static bool TryGetActorUserId(UserManager<ApplicationUser> userManager, HttpContext httpContext, out Guid userId)
    {
        var raw = userManager.GetUserId(httpContext.User);
        return Guid.TryParse(raw, out userId) && userId != Guid.Empty;
    }

    private static IResult Unauthenticated(HttpContext httpContext) =>
        ProblemResults.Create(httpContext, StatusCodes.Status401Unauthorized, "unauthenticated", "Authentication required", "Sign in to continue.");

    private static IResult Forbidden(HttpContext httpContext, string errorCode, string detail) =>
        ProblemResults.Create(httpContext, StatusCodes.Status403Forbidden, errorCode, "Forbidden", detail);

    private static IResult MapScopeDenial(HttpContext httpContext, ScopeDenialReason reason, string? errorCode) =>
        reason switch
        {
            ScopeDenialReason.InsufficientPermission => Forbidden(httpContext, errorCode ?? "insufficient_entity_permission", "Your active grants for this legal entity do not permit this action."),
            ScopeDenialReason.EntityNotFound => ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, errorCode ?? "entity_not_found", "Entity not found", "The requested legal entity does not exist or is outside your active scope."),
            _ => ProblemResults.Create(httpContext, StatusCodes.Status404NotFound, errorCode ?? "workspace_not_found", "Workspace not found", "The requested tenant workspace does not exist or is outside your active scope.")
        };
}
