using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Identity;

namespace Orphac.Athera.UnitTests;

public sealed class S02AdminApprovalOutboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UserInvitation_Create_HashesTokenAndEnforcesExpiryAndStateTransitions()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var acceptorId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            UserInvitation.Create(Guid.NewGuid(), tenantId, entityId, "user@demo.local", RoleTemplates.ProcurementOfficer, inviterId, Now, Now));

        var (invitation, rawToken) = UserInvitation.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            "  Staff@Demo.Athera.Local ",
            RoleTemplates.ProcurementOfficer,
            inviterId,
            Now.AddHours(48),
            Now);

        Assert.Equal("staff@demo.athera.local", invitation.InvitedEmail);
        Assert.Equal(InvitationStatuses.Pending, invitation.Status);
        Assert.NotEqual(rawToken, invitation.TokenHash);
        Assert.Equal(64, invitation.TokenHash.Length);
        Assert.Equal(UserInvitation.ComputeTokenHash(rawToken), invitation.TokenHash);
        Assert.False(invitation.IsExpiredAt(Now.AddHours(47)));
        Assert.True(invitation.IsExpiredAt(Now.AddHours(48)));

        invitation.Accept(acceptorId, Now.AddMinutes(5));
        Assert.Equal(InvitationStatuses.Accepted, invitation.Status);
        Assert.Equal(acceptorId, invitation.AcceptedByUserId);
        Assert.Throws<InvalidOperationException>(() => invitation.Revoke(Now.AddMinutes(6)));
    }

    [Fact]
    public void ApprovalCaseAndDecision_ForbidSelfApprovalAndDoubleDecision()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var approvalCase = ApprovalCase.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            policyId,
            ApprovalActionTypes.EntityAccessGrantRequest,
            requesterId,
            PermissionCodes.InventoryRead,
            "Need stock visibility for audit.",
            requesterId,
            Now);

        Assert.Equal(ApprovalCaseStatuses.Pending, approvalCase.Status);

        // Self-approval rejected in domain
        Assert.Throws<InvalidOperationException>(() =>
            approvalCase.RecordDecision(requesterId, ApprovalDecisions.Approved, "Self-approve attempt", Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() =>
            ApprovalDecision.Create(Guid.NewGuid(), tenantId, entityId, approvalCase.CaseId, requesterId, requesterId, ApprovalDecisions.Approved, "Self-approve", true, Now));

        // Blank decision reason rejected
        Assert.Throws<ArgumentException>(() =>
            approvalCase.RecordDecision(approverId, ApprovalDecisions.Approved, "   ", Now.AddMinutes(2)));

        approvalCase.RecordDecision(approverId, ApprovalDecisions.Approved, "Approved by distinct controller", Now.AddMinutes(3));
        Assert.Equal(ApprovalCaseStatuses.Approved, approvalCase.Status);
        Assert.Equal(approverId, approvalCase.DecidedByUserId);
        Assert.Equal(2L, approvalCase.Version);

        // Double decision rejected
        Assert.Throws<InvalidOperationException>(() =>
            approvalCase.RecordDecision(approverId, ApprovalDecisions.Rejected, "Too late", Now.AddMinutes(4)));
    }

    [Fact]
    public void PermissionCodesAndRoleTemplates_RejectEscalatedOrUnknownPermissions()
    {
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.AdminManage));
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.ApprovalRequest));
        Assert.True(PermissionCodes.IsKnown(PermissionCodes.ApprovalDecide));
        Assert.False(PermissionCodes.IsKnown("superadmin.root"));
        Assert.Throws<ArgumentException>(() => PermissionCodes.NormalizeKnown("superadmin.root"));

        var procurementPerms = RoleTemplates.GetPermissions(RoleTemplates.ProcurementOfficer);
        Assert.Contains(PermissionCodes.WorkspaceView, procurementPerms);
        Assert.Contains(PermissionCodes.ProcurementRead, procurementPerms);
        Assert.DoesNotContain(PermissionCodes.AdminManage, procurementPerms);

        Assert.Throws<ArgumentException>(() => RoleTemplates.GetPermissions("GOD_MODE"));
    }
}
