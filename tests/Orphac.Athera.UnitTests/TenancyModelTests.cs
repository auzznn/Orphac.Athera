using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.UnitTests;

public sealed class TenancyModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tenant_Create_NormalizesCodeAndNameAndDefaultsToActive()
    {
        var id = Guid.NewGuid();
        var tenant = Tenant.Create(id, "  nusa  ", "  PT Nusantara Niaga Utama  ", Now);

        Assert.Equal(id, tenant.TenantId);
        Assert.Equal("NUSA", tenant.Code);
        Assert.Equal("PT Nusantara Niaga Utama", tenant.Name);
        Assert.Equal(TenantStatuses.Active, tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Equal(1L, tenant.Version);
    }

    [Fact]
    public void Tenant_Create_RejectsInvalidIdentifiersAndBlankFields()
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.Empty, "NUSA", "PT Nusantara", Now));
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), "   ", "PT Nusantara", Now));
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), "NUSA", "  ", Now));
    }

    [Fact]
    public void LegalEntity_Create_DefaultsIndonesianContextAndValidatesCodes()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var entity = LegalEntity.Create(
            entityId,
            tenantId,
            " nusa-jkt ",
            " PT Nusantara Distribusi Jakarta ",
            "01.234.567.8-901.000",
            null,
            null,
            null,
            Now);

        Assert.Equal(entityId, entity.LegalEntityId);
        Assert.Equal(tenantId, entity.TenantId);
        Assert.Equal("NUSA-JKT", entity.Code);
        Assert.Equal("PT Nusantara Distribusi Jakarta", entity.Name);
        Assert.Equal("01.234.567.8-901.000", entity.TaxRegistrationNpwp);
        Assert.Equal("IDR", entity.FunctionalCurrencyCode);
        Assert.Equal("ID", entity.CountryCode);
        Assert.Equal("Asia/Jakarta", entity.TimeZoneId);
        Assert.Equal(LegalEntityStatuses.Active, entity.Status);
        Assert.True(entity.IsActive);
    }

    [Fact]
    public void LegalEntity_Create_RejectsEmptyTenantOrInvalidIsoCodes()
    {
        var tenantId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
            LegalEntity.Create(Guid.NewGuid(), Guid.Empty, "JKT", "Jakarta", null, "IDR", "ID", "Asia/Jakarta", Now));
        Assert.Throws<ArgumentException>(() =>
            LegalEntity.Create(Guid.NewGuid(), tenantId, "JKT", "Jakarta", null, "ID", "ID", "Asia/Jakarta", Now));
        Assert.Throws<ArgumentException>(() =>
            LegalEntity.Create(Guid.NewGuid(), tenantId, "JKT", "Jakarta", null, "IDR", "IDN", "Asia/Jakarta", Now));
    }

    [Fact]
    public void TenantMembership_SuspendAndRevokeTransitionStateAndRecordRevocationTimestamp()
    {
        var membership = TenantMembership.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True(membership.IsActive);
        Assert.Null(membership.RevokedAtUtc);

        var t1 = Now.AddMinutes(5);
        membership.Suspend(t1);
        Assert.Equal(MembershipStatuses.Suspended, membership.Status);
        Assert.False(membership.IsActive);
        Assert.Equal(2L, membership.Version);

        var t2 = Now.AddMinutes(10);
        membership.Revoke(t2);
        Assert.Equal(MembershipStatuses.Revoked, membership.Status);
        Assert.Equal(t2, membership.RevokedAtUtc);
        Assert.Equal(3L, membership.Version);
    }

    [Fact]
    public void EntityAccessGrant_EnforcesValidityWindowAndStatusIndependently()
    {
        var tenantId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            EntityAccessGrant.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                userId,
                PermissionCodes.WorkspaceView,
                Now,
                Now,
                Now));

        Assert.Throws<ArgumentException>(() =>
            EntityAccessGrant.Create(
                Guid.NewGuid(),
                tenantId,
                entityId,
                userId,
                PermissionCodes.WorkspaceView,
                Now,
                Now.AddMinutes(-1),
                Now));

        var grant = EntityAccessGrant.Create(
            Guid.NewGuid(),
            tenantId,
            entityId,
            userId,
            " WORKSPACE.VIEW ",
            Now,
            Now.AddHours(2),
            Now);

        Assert.Equal(PermissionCodes.WorkspaceView, grant.PermissionCode);
        Assert.False(grant.IsEffectiveAt(Now.AddSeconds(-1)));
        Assert.True(grant.IsEffectiveAt(Now));
        Assert.True(grant.IsEffectiveAt(Now.AddHours(1)));
        Assert.False(grant.IsEffectiveAt(Now.AddHours(2)));

        grant.Revoke(Now.AddMinutes(10));
        Assert.Equal(GrantStatuses.Revoked, grant.Status);
        Assert.False(grant.IsEffectiveAt(Now.AddMinutes(15)));
    }
}
