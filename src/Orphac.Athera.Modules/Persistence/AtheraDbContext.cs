using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.Modules.Persistence;

/// <summary>
/// Shared transactional EF Core DbContext partitioned by module-owned PostgreSQL schemas.
/// </summary>
public sealed class AtheraDbContext(DbContextOptions<AtheraDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<EntityAccessGrant> EntityAccessGrants => Set<EntityAccessGrant>();
    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();
    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();
    public DbSet<ApprovalCase> ApprovalCases => Set<ApprovalCase>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxReceipt> InboxReceipts => Set<InboxReceipt>();
    public DbSet<CommandIdempotency> CommandIdempotencies => Set<CommandIdempotency>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<PartnerContact> PartnerContacts => Set<PartnerContact>();
    public DbSet<PartnerEntityProfile> PartnerEntityProfiles => Set<PartnerEntityProfile>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemUom> ItemUomConversions => Set<ItemUom>();
    public DbSet<ItemEntityPolicy> ItemEntityPolicies => Set<ItemEntityPolicy>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("asp_net_users", "identity");
        builder.Entity<ApplicationRole>().ToTable("asp_net_roles", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens", "identity");

        builder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("user_accounts", "identity", t =>
            {
                t.HasCheckConstraint("ck_user_accounts_locale", "preferred_locale IN ('id-ID', 'en-US')");
            });

            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.PreferredLocale).HasColumnName("preferred_locale").HasMaxLength(16).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.LastSignedInAtUtc).HasColumnName("last_signed_in_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

            entity.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserAccount>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants", "platform", t =>
            {
                t.HasCheckConstraint("ck_tenants_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
            });

            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").ValueGeneratedNever();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<LegalEntity>(entity =>
        {
            entity.ToTable("legal_entities", "platform", t =>
            {
                t.HasCheckConstraint("ck_legal_entities_status", "status IN ('ACTIVE', 'SUSPENDED', 'ARCHIVED')");
            });

            entity.HasKey(x => x.LegalEntityId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId });

            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.TaxRegistrationNpwp).HasColumnName("tax_registration_npwp").HasMaxLength(64);
            entity.Property(x => x.FunctionalCurrencyCode).HasColumnName("functional_currency_code").HasColumnType("char(3)").HasDefaultValue("IDR").IsRequired();
            entity.Property(x => x.CountryCode).HasColumnName("country_code").HasColumnType("char(2)").HasDefaultValue("ID").IsRequired();
            entity.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(64).HasDefaultValue("Asia/Jakarta").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("tenant_memberships", "identity", t =>
            {
                t.HasCheckConstraint("ck_tenant_memberships_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
            });

            entity.HasKey(x => x.MembershipId);
            entity.HasAlternateKey(x => new { x.TenantId, x.UserId });

            entity.Property(x => x.MembershipId).HasColumnName("membership_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.UserId, x.MembershipId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EntityAccessGrant>(entity =>
        {
            entity.ToTable("entity_access_grants", "identity", t =>
            {
                t.HasCheckConstraint("ck_entity_access_grants_status", "status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')");
                t.HasCheckConstraint("ck_entity_access_grants_validity_window", "valid_until_utc IS NULL OR valid_until_utc > valid_from_utc");
            });

            entity.HasKey(x => x.GrantId);

            entity.Property(x => x.GrantId).HasColumnName("grant_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.PermissionCode).HasColumnName("permission_code").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ValidFromUtc).HasColumnName("valid_from_utc").IsRequired();
            entity.Property(x => x.ValidUntilUtc).HasColumnName("valid_until_utc");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UserId, x.PermissionCode })
                .IsUnique()
                .HasFilter("status <> 'REVOKED'");
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.LegalEntityId, x.Status });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.UserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserInvitation>(entity =>
        {
            entity.ToTable("user_invitations", "identity", t =>
            {
                t.HasCheckConstraint("ck_user_invitations_status", "status IN ('PENDING', 'ACCEPTED', 'REVOKED', 'EXPIRED')");
                t.HasCheckConstraint("ck_user_invitations_expiry", "expires_at_utc > created_at_utc");
            });

            entity.HasKey(x => x.InvitationId);
            entity.Property(x => x.InvitationId).HasColumnName("invitation_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.InvitedEmail).HasColumnName("invited_email").HasMaxLength(256).IsRequired();
            entity.Property(x => x.RoleTemplateCode).HasColumnName("role_template_code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.InvitedByUserId).HasColumnName("invited_by_user_id").IsRequired();
            entity.Property(x => x.AcceptedByUserId).HasColumnName("accepted_by_user_id");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.AcceptedAtUtc).HasColumnName("accepted_at_utc");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.InvitedEmail, x.Status });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.InvitedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovalPolicy>(entity =>
        {
            entity.ToTable("approval_policies", "approval");
            entity.HasKey(x => x.PolicyId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyId });

            entity.Property(x => x.PolicyId).HasColumnName("policy_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PolicyCode).HasColumnName("policy_code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.RequiresFourEyes).HasColumnName("requires_four_eyes").IsRequired();
            entity.Property(x => x.RequiresApproverMfa).HasColumnName("requires_approver_mfa").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyCode }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovalCase>(entity =>
        {
            entity.ToTable("approval_cases", "approval", t =>
            {
                t.HasCheckConstraint("ck_approval_cases_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                t.HasCheckConstraint("ck_approval_cases_no_self_decision", "decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id");
            });

            entity.HasKey(x => x.CaseId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.CaseId });

            entity.Property(x => x.CaseId).HasColumnName("case_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PolicyId).HasColumnName("policy_id").IsRequired();
            entity.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(64).IsRequired();
            entity.Property(x => x.TargetUserId).HasColumnName("target_user_id").IsRequired();
            entity.Property(x => x.TargetPermissionCode).HasColumnName("target_permission_code").HasMaxLength(100).IsRequired();
            entity.Property(x => x.RequestReason).HasColumnName("request_reason").HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
            entity.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
            entity.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
            entity.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.RequestedAtUtc });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ApprovalPolicy>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyId })
                .HasPrincipalKey(p => new { p.TenantId, p.LegalEntityId, p.PolicyId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.RequestedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApprovalDecision>(entity =>
        {
            entity.ToTable("approval_decisions", "approval", t =>
            {
                t.HasCheckConstraint("ck_approval_decisions_decision", "decision IN ('APPROVED', 'REJECTED')");
                t.HasCheckConstraint("ck_approval_decisions_four_eyes", "decided_by_user_id <> requested_by_user_id");
            });

            entity.HasKey(x => x.DecisionId);
            entity.Property(x => x.DecisionId).HasColumnName("decision_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.CaseId).HasColumnName("case_id").IsRequired();
            entity.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
            entity.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id").IsRequired();
            entity.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
            entity.Property(x => x.ApproverMfaVerified).HasColumnName("approver_mfa_verified").IsRequired();
            entity.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CaseId }).IsUnique();

            entity.HasOne<ApprovalCase>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CaseId })
                .HasPrincipalKey(c => new { c.TenantId, c.LegalEntityId, c.CaseId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.DecidedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events", "audit");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ActorUserId).HasColumnName("actor_user_id").IsRequired();
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80).IsRequired();
            entity.Property(x => x.SubjectType).HasColumnName("subject_type").HasMaxLength(64).IsRequired();
            entity.Property(x => x.SubjectId).HasColumnName("subject_id").HasMaxLength(128).IsRequired();
            entity.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(300).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OccurredAtUtc });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages", "integration", t =>
            {
                t.HasCheckConstraint("ck_outbox_messages_status", "status IN ('PENDING', 'PROCESSED', 'FAILED')");
            });

            entity.HasKey(x => x.MessageId);
            entity.HasAlternateKey(x => new { x.TenantId, x.MessageId });

            entity.Property(x => x.MessageId).HasColumnName("message_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.Topic).HasColumnName("topic").HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceKey).HasColumnName("source_key").HasMaxLength(160).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0).IsRequired();
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc").IsRequired();
            entity.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.Topic, x.SourceKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status, x.NextAttemptAtUtc });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InboxReceipt>(entity =>
        {
            entity.ToTable("inbox_receipts", "integration");
            entity.HasKey(x => x.ReceiptId);
            entity.Property(x => x.ReceiptId).HasColumnName("receipt_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.ConsumerName).HasColumnName("consumer_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
            entity.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.ConsumerName, x.MessageId }).IsUnique();

            entity.HasOne<OutboxMessage>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.MessageId })
                .HasPrincipalKey(m => new { m.TenantId, m.MessageId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CommandIdempotency>(entity =>
        {
            entity.ToTable("command_idempotency", "integration");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.OperationCode).HasColumnName("operation_code").HasMaxLength(80).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
            entity.Property(x => x.ActorUserId).HasColumnName("actor_user_id").IsRequired();
            entity.Property(x => x.ResourceId).HasColumnName("resource_id").HasMaxLength(128).IsRequired();
            entity.Property(x => x.StatusCode).HasColumnName("status_code").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OperationCode, x.IdempotencyKey }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Partner>(entity =>
        {
            entity.ToTable("partners", "partners", t =>
            {
                t.HasCheckConstraint("ck_partners_role", "is_supplier OR is_customer");
                t.HasCheckConstraint("ck_partners_tax_pkp", "tax_pkp_status IN ('UNKNOWN', 'PKP', 'NON_PKP')");
                t.HasCheckConstraint("ck_partners_scope", "entity_scope_mode IN ('ALL_ENTITIES', 'RESTRICTED_ENTITIES')");
                t.HasCheckConstraint("ck_partners_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.PartnerId);
            entity.HasAlternateKey(x => new { x.TenantId, x.PartnerId });

            entity.Property(x => x.PartnerId).HasColumnName("partner_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.PartnerCode).HasColumnName("partner_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.IsSupplier).HasColumnName("is_supplier").IsRequired();
            entity.Property(x => x.IsCustomer).HasColumnName("is_customer").IsRequired();
            entity.Property(x => x.TaxRegistrationNpwp).HasColumnName("tax_registration_npwp").HasMaxLength(64);
            entity.Property(x => x.TaxPkpStatus).HasColumnName("tax_pkp_status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.EntityScopeMode).HasColumnName("entity_scope_mode").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.DeactivatedAtUtc).HasColumnName("deactivated_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.PartnerCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PartnerContact>(entity =>
        {
            entity.ToTable("partner_contacts", "partners", t =>
            {
                t.HasCheckConstraint("ck_partner_contacts_role", "contact_role IN ('COMMERCIAL', 'BILLING', 'RECEIVING_DISPATCH')");
                t.HasCheckConstraint("ck_partner_contacts_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.ContactId);
            entity.Property(x => x.ContactId).HasColumnName("contact_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.PartnerId).HasColumnName("partner_id").IsRequired();
            entity.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.ContactRole).HasColumnName("contact_role").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(64);
            entity.Property(x => x.IsPrimary).HasColumnName("is_primary").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.PartnerId });

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.PartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PartnerEntityProfile>(entity =>
        {
            entity.ToTable("partner_entity_profiles", "partners", t =>
            {
                t.HasCheckConstraint("ck_partner_entity_profiles_terms", "default_payment_terms_days BETWEEN 0 AND 365");
            });

            entity.HasKey(x => x.ProfileId);
            entity.Property(x => x.ProfileId).HasColumnName("profile_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PartnerId).HasColumnName("partner_id").IsRequired();
            entity.Property(x => x.SupplierEligible).HasColumnName("supplier_eligible").IsRequired();
            entity.Property(x => x.CustomerEligible).HasColumnName("customer_eligible").IsRequired();
            entity.Property(x => x.DefaultPaymentTermsDays).HasColumnName("default_payment_terms_days").HasDefaultValue(0).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PartnerId }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.PartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("units_of_measure", "catalog", t =>
            {
                t.HasCheckConstraint("ck_uoms_scale", "quantity_precision_scale BETWEEN 0 AND 6");
                t.HasCheckConstraint("ck_uoms_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.UomId);
            entity.HasAlternateKey(x => new { x.TenantId, x.UomId });

            entity.Property(x => x.UomId).HasColumnName("uom_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.UomCode).HasColumnName("uom_code").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.QuantityPrecisionScale).HasColumnName("quantity_precision_scale").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.UomCode }).IsUnique();

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Item>(entity =>
        {
            entity.ToTable("items", "catalog", t =>
            {
                t.HasCheckConstraint("ck_items_kind", "item_kind IN ('STOCKED_GOOD', 'NON_STOCK_SERVICE')");
                t.HasCheckConstraint("ck_items_scope", "entity_scope_mode IN ('ALL_ENTITIES', 'RESTRICTED_ENTITIES')");
                t.HasCheckConstraint("ck_items_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.ItemId);
            entity.HasAlternateKey(x => new { x.TenantId, x.ItemId });

            entity.Property(x => x.ItemId).HasColumnName("item_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.ItemCode).HasColumnName("item_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.ItemKind).HasColumnName("item_kind").HasMaxLength(24).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.DefaultPurchaseUomId).HasColumnName("default_purchase_uom_id").IsRequired();
            entity.Property(x => x.DefaultSalesUomId).HasColumnName("default_sales_uom_id").IsRequired();
            entity.Property(x => x.EntityScopeMode).HasColumnName("entity_scope_mode").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.DeactivatedAtUtc).HasColumnName("deactivated_at_utc");
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.ItemCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });

            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.DefaultPurchaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.DefaultSalesUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ItemUom>(entity =>
        {
            entity.ToTable("item_uom_conversions", "catalog", t =>
            {
                t.HasCheckConstraint("ck_item_uoms_factor", "factor_to_base > 0");
                t.HasCheckConstraint("ck_item_uoms_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.ItemUomId);
            entity.Property(x => x.ItemUomId).HasColumnName("item_uom_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.UomId).HasColumnName("uom_id").IsRequired();
            entity.Property(x => x.FactorToBase).HasColumnName("factor_to_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.ItemId, x.UomId }).IsUnique();

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.UomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ItemEntityPolicy>(entity =>
        {
            entity.ToTable("item_entity_policies", "catalog");

            entity.HasKey(x => x.PolicyId);
            entity.Property(x => x.PolicyId).HasColumnName("policy_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.PurchaseAllowed).HasColumnName("purchase_allowed").IsRequired();
            entity.Property(x => x.SalesAllowed).HasColumnName("sales_allowed").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
