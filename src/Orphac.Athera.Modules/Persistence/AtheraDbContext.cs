using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Audit;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Integration;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Quality;
using Orphac.Athera.Modules.Tax;

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
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<FinancialPeriod> FinancialPeriods => Set<FinancialPeriod>();
    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();
    public DbSet<JournalSequence> JournalSequences => Set<JournalSequence>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<DocumentApproval> DocumentApprovals => Set<DocumentApproval>();
    public DbSet<DeliveryTolerancePolicy> DeliveryTolerancePolicies => Set<DeliveryTolerancePolicy>();
    public DbSet<PoSequence> PoSequences => Set<PoSequence>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderRevision> PurchaseOrderRevisions => Set<PurchaseOrderRevision>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<ReceiptSequence> ReceiptSequences => Set<ReceiptSequence>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<DockCheck> DockChecks => Set<DockCheck>();
    public DbSet<AcceptanceSequence> AcceptanceSequences => Set<AcceptanceSequence>();
    public DbSet<ValuedAcceptance> ValuedAcceptances => Set<ValuedAcceptance>();
    public DbSet<ValuedAcceptanceLine> ValuedAcceptanceLines => Set<ValuedAcceptanceLine>();
    public DbSet<MovementSequence> MovementSequences => Set<MovementSequence>();
    public DbSet<StockPosition> StockPositions => Set<StockPosition>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<MovementLine> MovementLines => Set<MovementLine>();
    public DbSet<ValuationPool> ValuationPools => Set<ValuationPool>();
    public DbSet<ValuationEntry> ValuationEntries => Set<ValuationEntry>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<StockLot> StockLots => Set<StockLot>();
    public DbSet<StockLocationBalance> StockLocationBalances => Set<StockLocationBalance>();
    public DbSet<ReservationSequence> ReservationSequences => Set<ReservationSequence>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<InspectionSequence> InspectionSequences => Set<InspectionSequence>();
    public DbSet<ReceiptInspection> ReceiptInspections => Set<ReceiptInspection>();
    public DbSet<InspectionDisposition> InspectionDispositions => Set<InspectionDisposition>();
    public DbSet<ReceiptAccrual> ReceiptAccruals => Set<ReceiptAccrual>();
    public DbSet<ReceiptAccrualLine> ReceiptAccrualLines => Set<ReceiptAccrualLine>();
    public DbSet<ReceiptAccrualClearing> ReceiptAccrualClearings => Set<ReceiptAccrualClearing>();
    public DbSet<InvoicePriceAdjustment> InvoicePriceAdjustments => Set<InvoicePriceAdjustment>();
    public DbSet<TaxDetermination> TaxDeterminations => Set<TaxDetermination>();
    public DbSet<BillSequence> BillSequences => Set<BillSequence>();
    public DbSet<PayableSequence> PayableSequences => Set<PayableSequence>();
    public DbSet<InvoiceMatchPolicy> InvoiceMatchPolicies => Set<InvoiceMatchPolicy>();
    public DbSet<SupplierBill> SupplierBills => Set<SupplierBill>();
    public DbSet<SupplierBillLine> SupplierBillLines => Set<SupplierBillLine>();
    public DbSet<InvoiceMatchEvaluation> InvoiceMatchEvaluations => Set<InvoiceMatchEvaluation>();
    public DbSet<SupplierBillReceiptMatch> SupplierBillReceiptMatches => Set<SupplierBillReceiptMatch>();
    public DbSet<PayableItem> PayableItems => Set<PayableItem>();
    public DbSet<ReturnSequence> ReturnSequences => Set<ReturnSequence>();
    public DbSet<ReplacementSequence> ReplacementSequences => Set<ReplacementSequence>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierReturnLine> SupplierReturnLines => Set<SupplierReturnLine>();
    public DbSet<ReplacementAuthorization> ReplacementAuthorizations => Set<ReplacementAuthorization>();
    public DbSet<CreditSequence> CreditSequences => Set<CreditSequence>();
    public DbSet<SupplierCredit> SupplierCredits => Set<SupplierCredit>();
    public DbSet<SupplierCreditLine> SupplierCreditLines => Set<SupplierCreditLine>();

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
                t.HasCheckConstraint("ck_items_lot_tracking_policy", "lot_tracking_policy IN ('NONE', 'OPTIONAL', 'REQUIRED') AND (item_kind = 'STOCKED_GOOD' OR lot_tracking_policy = 'NONE')");
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
            entity.Property(x => x.LotTrackingPolicy).HasColumnName("lot_tracking_policy").HasMaxLength(16).HasDefaultValue(LotTrackingPolicies.None).IsRequired();
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

        builder.Entity<GlAccount>(entity =>
        {
            entity.ToTable("accounts", "gl", t =>
            {
                t.HasCheckConstraint("ck_gl_accounts_type", "account_type IN ('ASSET', 'LIABILITY', 'EQUITY', 'REVENUE', 'EXPENSE')");
                t.HasCheckConstraint("ck_gl_accounts_normal_balance", "normal_balance IN ('DEBIT', 'CREDIT')");
                t.HasCheckConstraint("ck_gl_accounts_status", "status IN ('ACTIVE', 'INACTIVE')");
                t.HasCheckConstraint("ck_gl_accounts_header_no_post", "account_category <> 'HEADER_GROUP' OR allow_posting = false");
                t.HasCheckConstraint("ck_gl_accounts_no_self_parent", "parent_account_id IS NULL OR parent_account_id <> account_id");
            });

            entity.HasKey(x => x.AccountId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.AccountId });

            entity.Property(x => x.AccountId).HasColumnName("account_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.AccountCode).HasColumnName("account_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.AccountType).HasColumnName("account_type").HasMaxLength(16).IsRequired();
            entity.Property(x => x.AccountCategory).HasColumnName("account_category").HasMaxLength(40).IsRequired();
            entity.Property(x => x.NormalBalance).HasColumnName("normal_balance").HasMaxLength(12).IsRequired();
            entity.Property(x => x.ParentAccountId).HasColumnName("parent_account_id");
            entity.Property(x => x.AllowPosting).HasColumnName("allow_posting").IsRequired();
            entity.Property(x => x.IsControlAccount).HasColumnName("is_control_account").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AccountCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AccountType, x.Status });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ParentAccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialPeriod>(entity =>
        {
            entity.ToTable("financial_periods", "gl", t =>
            {
                t.HasCheckConstraint("ck_gl_periods_dates", "end_date >= start_date");
                t.HasCheckConstraint("ck_gl_periods_status", "status IN ('OPEN', 'CLOSED', 'LOCKED')");
                t.HasCheckConstraint("ck_gl_periods_number", "period_number >= 1 AND period_number <= 13");
                t.HasCheckConstraint("ck_gl_periods_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
            });

            entity.HasKey(x => x.PeriodId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PeriodId });

            entity.Property(x => x.PeriodId).HasColumnName("period_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PeriodCode).HasColumnName("period_code").HasMaxLength(24).IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.PeriodNumber).HasColumnName("period_number").IsRequired();
            entity.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc");
            entity.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
            entity.Property(x => x.CloseNote).HasColumnName("close_note").HasMaxLength(300);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PeriodCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear, x.PeriodNumber }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountMapping>(entity =>
        {
            entity.ToTable("account_mappings", "gl");

            entity.HasKey(x => x.MappingId);
            entity.Property(x => x.MappingId).HasColumnName("mapping_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PurposeCode).HasColumnName("purpose_code").HasMaxLength(48).IsRequired();
            entity.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurposeCode }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.AccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JournalSequence>(entity =>
        {
            entity.ToTable("journal_sequences", "gl", t =>
            {
                t.HasCheckConstraint("ck_gl_journal_sequences_nonnegative", "last_number >= 0");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.PeriodId });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PeriodId).HasColumnName("period_id").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<FinancialPeriod>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PeriodId })
                .HasPrincipalKey(p => new { p.TenantId, p.LegalEntityId, p.PeriodId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries", "gl", t =>
            {
                t.HasCheckConstraint("ck_gl_journals_kind", "entry_kind IN ('STANDARD', 'REVERSAL')");
                t.HasCheckConstraint("ck_gl_journals_balanced", "total_debit > 0 AND total_debit = total_credit");
                t.HasCheckConstraint("ck_gl_journals_line_count", "line_count >= 2");
                t.HasCheckConstraint("ck_gl_journals_no_self_reversal", "reversal_of_journal_id IS NULL OR reversal_of_journal_id <> journal_id");
            });

            entity.HasKey(x => x.JournalId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.JournalId });

            entity.Property(x => x.JournalId).HasColumnName("journal_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PeriodId).HasColumnName("period_id").IsRequired();
            entity.Property(x => x.JournalNumber).HasColumnName("journal_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.EntryKind).HasColumnName("entry_kind").HasMaxLength(16).IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(300).IsRequired();
            entity.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").HasMaxLength(100).IsRequired();
            entity.Property(x => x.SourceEventKey).HasColumnName("source_event_key").HasMaxLength(160).IsRequired();
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReversalOfJournalId).HasColumnName("reversal_of_journal_id");
            entity.Property(x => x.TotalDebit).HasColumnName("total_debit").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCredit).HasColumnName("total_credit").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.LineCount).HasColumnName("line_count").IsRequired();
            entity.Property(x => x.PostedByUserId).HasColumnName("posted_by_user_id").IsRequired();
            entity.Property(x => x.PostedAtUtc).HasColumnName("posted_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.JournalNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceModule, x.SourceEventKey })
                .IsUnique()
                .HasDatabaseName("ux_journal_entries_source_event");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReversalOfJournalId })
                .IsUnique()
                .HasDatabaseName("ux_journal_entries_single_reversal")
                .HasFilter("reversal_of_journal_id IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PostingDate });

            entity.HasOne<FinancialPeriod>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PeriodId })
                .HasPrincipalKey(p => new { p.TenantId, p.LegalEntityId, p.PeriodId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReversalOfJournalId })
                .HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaxCode>(entity =>
        {
            entity.ToTable("tax_codes", "tax", t =>
            {
                t.HasCheckConstraint("ck_tax_codes_family", "tax_family IN ('PPN', 'PPH')");
                t.HasCheckConstraint("ck_tax_codes_direction", "direction IN ('INPUT', 'OUTPUT', 'WITHHOLDING')");
                t.HasCheckConstraint("ck_tax_codes_treatment", "treatment_class IN ('STANDARD_TAXABLE', 'SPECIAL_DPP_11_12', 'ZERO_RATED', 'EXEMPT', 'NOT_COLLECTED_FACILITY', 'WITHHOLDING_DEDUCTION')");
                t.HasCheckConstraint("ck_tax_codes_rate", "nominal_rate_percent >= 0 AND nominal_rate_percent <= 100");
                t.HasCheckConstraint("ck_tax_codes_ratio", "taxable_base_ratio_numerator >= 1 AND taxable_base_ratio_denominator >= taxable_base_ratio_numerator");
                t.HasCheckConstraint("ck_tax_codes_dates", "effective_to IS NULL OR effective_to >= effective_from");
                t.HasCheckConstraint("ck_tax_codes_review", "review_status IN ('UNREVIEWED_CONFIGURABLE', 'REVIEWED_INTERNAL')");
                t.HasCheckConstraint("ck_tax_codes_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.TaxCodeId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.TaxCodeId });

            entity.Property(x => x.TaxCodeId).HasColumnName("tax_code_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.TaxFamily).HasColumnName("tax_family").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(16).IsRequired();
            entity.Property(x => x.TreatmentClass).HasColumnName("treatment_class").HasMaxLength(32).IsRequired();
            entity.Property(x => x.NominalRatePercent).HasColumnName("nominal_rate_percent").HasColumnType("numeric(7,4)").IsRequired();
            entity.Property(x => x.TaxableBaseRatioNumerator).HasColumnName("taxable_base_ratio_numerator").IsRequired();
            entity.Property(x => x.TaxableBaseRatioDenominator).HasColumnName("taxable_base_ratio_denominator").IsRequired();
            entity.Property(x => x.RequiresDocumentaryEvidence).HasColumnName("requires_documentary_evidence").IsRequired();
            entity.Property(x => x.RequiresCounterpartyNpwp).HasColumnName("requires_counterparty_npwp").IsRequired();
            entity.Property(x => x.TaxAccountId).HasColumnName("tax_account_id");
            entity.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            entity.Property(x => x.EffectiveTo).HasColumnName("effective_to");
            entity.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.StatutoryReferenceNote).HasColumnName("statutory_reference_note").HasMaxLength(300);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code, x.EffectiveFrom }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TaxAccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JournalLine>(entity =>
        {
            entity.ToTable("journal_lines", "gl", t =>
            {
                t.HasCheckConstraint("ck_gl_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_gl_lines_nonnegative", "debit_amount >= 0 AND credit_amount >= 0");
                t.HasCheckConstraint("ck_gl_lines_single_sided", "(debit_amount > 0 AND credit_amount = 0) OR (credit_amount > 0 AND debit_amount = 0)");
            });

            entity.HasKey(x => x.LineId);

            entity.Property(x => x.LineId).HasColumnName("line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.JournalId).HasColumnName("journal_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(240);
            entity.Property(x => x.DebitAmount).HasColumnName("debit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.CreditAmount).HasColumnName("credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.TaxCodeId).HasColumnName("tax_code_id");

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.JournalId, x.LineNumber }).IsUnique();

            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.JournalId })
                .HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.AccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.PartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TaxCode>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TaxCodeId })
                .HasPrincipalKey(tc => new { tc.TenantId, tc.LegalEntityId, tc.TaxCodeId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DocumentApproval>(entity =>
        {
            entity.ToTable("document_approvals", "approval", t =>
            {
                t.HasCheckConstraint("ck_document_approvals_status", "status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')");
                t.HasCheckConstraint("ck_document_approvals_revision", "revision_number >= 1");
                t.HasCheckConstraint("ck_document_approvals_four_eyes", "decided_by_user_id IS NULL OR decided_by_user_id <> requested_by_user_id");
            });

            entity.HasKey(x => x.ApprovalId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovalId });

            entity.Property(x => x.ApprovalId).HasColumnName("approval_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PolicyId).HasColumnName("policy_id").IsRequired();
            entity.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.SourceRevisionId).HasColumnName("source_revision_id").IsRequired();
            entity.Property(x => x.RevisionNumber).HasColumnName("revision_number").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.RequestSummary).HasColumnName("request_summary").HasMaxLength(300).IsRequired();
            entity.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
            entity.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
            entity.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
            entity.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
            entity.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(500);
            entity.Property(x => x.ApproverMfaVerified).HasColumnName("approver_mfa_verified").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceModule, x.SourceDocumentType, x.SourceRevisionId })
                .IsUnique()
                .HasDatabaseName("ux_document_approvals_source_revision")
                .HasFilter("status IN ('PENDING', 'APPROVED')");
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

        builder.Entity<DeliveryTolerancePolicy>(entity =>
        {
            entity.ToTable("delivery_tolerance_policies", "procurement", t =>
            {
                t.HasCheckConstraint("ck_delivery_tolerance_nonnegative", "overdelivery_tolerance_percent >= 0 AND overdelivery_tolerance_base_qty >= 0");
                t.HasCheckConstraint("ck_delivery_tolerance_od01_safe", "overdelivery_tolerance_percent = 0 AND overdelivery_tolerance_base_qty = 0 AND allow_unlimited_overdelivery = false");
                t.HasCheckConstraint("ck_delivery_tolerance_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.PolicyId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyId });

            entity.Property(x => x.PolicyId).HasColumnName("policy_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(48).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(40).IsRequired();
            entity.Property(x => x.OverdeliveryTolerancePercent).HasColumnName("overdelivery_tolerance_percent").HasColumnType("numeric(7,4)").IsRequired();
            entity.Property(x => x.OverdeliveryToleranceBaseQty).HasColumnName("overdelivery_tolerance_base_qty").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AllowUnlimitedOverdelivery).HasColumnName("allow_unlimited_overdelivery").IsRequired();
            entity.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PoSequence>(entity =>
        {
            entity.ToTable("po_sequences", "procurement", t =>
            {
                t.HasCheckConstraint("ck_po_sequences_nonnegative", "last_number >= 0");
                t.HasCheckConstraint("ck_po_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders", "procurement", t =>
            {
                t.HasCheckConstraint("ck_purchase_orders_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'CLOSED', 'CANCELLED')");
                t.HasCheckConstraint("ck_purchase_orders_downstream_nonnegative", "downstream_reference_count >= 0 AND accepted_base_quantity_total >= 0");
                t.HasCheckConstraint("ck_purchase_orders_revisions_nonnegative", "latest_revision_number >= 1 AND current_approved_revision_number >= 0 AND current_approved_revision_number <= latest_revision_number");
                t.HasCheckConstraint("ck_purchase_orders_terms", "payment_terms_days >= 0 AND payment_terms_days <= 365");
            });

            entity.HasKey(x => x.PurchaseOrderId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId });

            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.DraftCode).HasColumnName("draft_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.PoNumber).HasColumnName("po_number").HasMaxLength(40);
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
            entity.Property(x => x.OrderDate).HasColumnName("order_date").IsRequired();
            entity.Property(x => x.ExpectedDeliveryDate).HasColumnName("expected_delivery_date");
            entity.Property(x => x.DeliveryDestination).HasColumnName("delivery_destination").HasMaxLength(200).IsRequired();
            entity.Property(x => x.PaymentTermsDays).HasColumnName("payment_terms_days").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.CurrentApprovedRevisionId).HasColumnName("current_approved_revision_id");
            entity.Property(x => x.CurrentApprovedRevisionNumber).HasColumnName("current_approved_revision_number").IsRequired();
            entity.Property(x => x.LatestRevisionNumber).HasColumnName("latest_revision_number").IsRequired();
            entity.Property(x => x.DownstreamReferenceCount).HasColumnName("downstream_reference_count").IsRequired();
            entity.Property(x => x.AcceptedBaseQuantityTotal).HasColumnName("accepted_base_quantity_total").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
            entity.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc");
            entity.Property(x => x.ClosureReason).HasColumnName("closure_reason").HasMaxLength(300);
            entity.Property(x => x.CancelledByUserId).HasColumnName("cancelled_by_user_id");
            entity.Property(x => x.CancelledAtUtc).HasColumnName("cancelled_at_utc");
            entity.Property(x => x.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(300);
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DraftCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PoNumber })
                .IsUnique()
                .HasDatabaseName("ux_purchase_orders_entity_po_number")
                .HasFilter("po_number IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.OrderDate });

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrderRevision>(entity =>
        {
            entity.ToTable("purchase_order_revisions", "procurement", t =>
            {
                t.HasCheckConstraint("ck_po_revisions_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'REJECTED', 'SUPERSEDED', 'CANCELLED')");
                t.HasCheckConstraint("ck_po_revisions_number", "revision_number >= 1");
                t.HasCheckConstraint("ck_po_revisions_four_eyes", "decided_by_user_id IS NULL OR submitted_by_user_id IS NULL OR decided_by_user_id <> submitted_by_user_id");
                t.HasCheckConstraint("ck_po_revisions_totals_nonnegative", "subtotal_amount >= 0 AND ppn_tax_total_amount >= 0 AND withholding_tax_total_amount >= 0 AND grand_total_amount >= 0 AND total_ordered_base_quantity >= 0");
            });

            entity.HasKey(x => x.RevisionId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.RevisionId });

            entity.Property(x => x.RevisionId).HasColumnName("revision_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.RevisionNumber).HasColumnName("revision_number").IsRequired();
            entity.Property(x => x.SupersedesRevisionId).HasColumnName("supersedes_revision_id");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.IsFrozen).HasColumnName("is_frozen").IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierNpwpSnapshot).HasColumnName("supplier_npwp_snapshot").HasMaxLength(64);
            entity.Property(x => x.SupplierPkpStatusSnapshot).HasColumnName("supplier_pkp_status_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasColumnType("char(3)").IsRequired();
            entity.Property(x => x.OrderDate).HasColumnName("order_date").IsRequired();
            entity.Property(x => x.ExpectedDeliveryDate).HasColumnName("expected_delivery_date");
            entity.Property(x => x.DeliveryDestination).HasColumnName("delivery_destination").HasMaxLength(200).IsRequired();
            entity.Property(x => x.PaymentTermsDays).HasColumnName("payment_terms_days").IsRequired();
            entity.Property(x => x.TolerancePolicyId).HasColumnName("tolerance_policy_id").IsRequired();
            entity.Property(x => x.ToleranceModeSnapshot).HasColumnName("tolerance_mode_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ChangeReason).HasColumnName("change_reason").HasMaxLength(300);
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(x => x.SubtotalAmount).HasColumnName("subtotal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnTaxTotalAmount).HasColumnName("ppn_tax_total_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.WithholdingTaxTotalAmount).HasColumnName("withholding_tax_total_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.GrandTotalAmount).HasColumnName("grand_total_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalOrderedBaseQuantity).HasColumnName("total_ordered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.SnapshotHash).HasColumnName("snapshot_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.SubmittedByUserId).HasColumnName("submitted_by_user_id");
            entity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc");
            entity.Property(x => x.ApprovalRecordId).HasColumnName("approval_record_id");
            entity.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
            entity.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
            entity.Property(x => x.DecisionReason).HasColumnName("decision_reason").HasMaxLength(500);
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.RevisionNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ApprovalRecordId })
                .IsUnique()
                .HasDatabaseName("ux_po_revisions_approval_record")
                .HasFilter("approval_record_id IS NOT NULL");

            entity.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId })
                .HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderRevision>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupersedesRevisionId })
                .HasPrincipalKey(r => new { r.TenantId, r.LegalEntityId, r.RevisionId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<DeliveryTolerancePolicy>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TolerancePolicyId })
                .HasPrincipalKey(tp => new { tp.TenantId, tp.LegalEntityId, tp.PolicyId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<DocumentApproval>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovalRecordId })
                .HasPrincipalKey(da => new { da.TenantId, da.LegalEntityId, da.ApprovalId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("purchase_order_lines", "procurement", t =>
            {
                t.HasCheckConstraint("ck_po_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_po_lines_positive_qty_price", "ordered_quantity > 0 AND ordered_base_quantity > 0 AND uom_factor_to_base_snapshot > 0 AND unit_price > 0 AND line_subtotal_amount > 0");
                t.HasCheckConstraint("ck_po_lines_od01_zero_tolerance", "overdelivery_tolerance_percent = 0 AND overdelivery_tolerance_base_qty = 0 AND allow_unlimited_overdelivery = false");
            });

            entity.HasKey(x => x.PoLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId });

            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.RevisionId).HasColumnName("revision_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.LineageLineKey).HasColumnName("lineage_line_key").HasMaxLength(48).IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ItemCodeSnapshot).HasColumnName("item_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ItemNameSnapshot).HasColumnName("item_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.ItemKindSnapshot).HasColumnName("item_kind_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.OrderedUomId).HasColumnName("ordered_uom_id").IsRequired();
            entity.Property(x => x.OrderedUomCodeSnapshot).HasColumnName("ordered_uom_code_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.UomFactorToBaseSnapshot).HasColumnName("uom_factor_to_base_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.OrderedQuantity).HasColumnName("ordered_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.OrderedBaseQuantity).HasColumnName("ordered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.LineSubtotalAmount).HasColumnName("line_subtotal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TaxCodeId).HasColumnName("tax_code_id");
            entity.Property(x => x.TaxCodeSnapshot).HasColumnName("tax_code_snapshot").HasMaxLength(32);
            entity.Property(x => x.TaxFamilySnapshot).HasColumnName("tax_family_snapshot").HasMaxLength(16);
            entity.Property(x => x.TaxRatePercentSnapshot).HasColumnName("tax_rate_percent_snapshot").HasColumnType("numeric(7,4)");
            entity.Property(x => x.TaxableBaseAmount).HasColumnName("taxable_base_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.OverdeliveryTolerancePercent).HasColumnName("overdelivery_tolerance_percent").HasColumnType("numeric(7,4)").IsRequired();
            entity.Property(x => x.OverdeliveryToleranceBaseQty).HasColumnName("overdelivery_tolerance_base_qty").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AllowUnlimitedOverdelivery).HasColumnName("allow_unlimited_overdelivery").IsRequired();
            entity.Property(x => x.LineDescription).HasColumnName("line_description").HasMaxLength(240);

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.LineNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.LineageLineKey }).IsUnique();

            entity.HasOne<PurchaseOrderRevision>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.RevisionId })
                .HasPrincipalKey(r => new { r.TenantId, r.LegalEntityId, r.RevisionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.OrderedUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TaxCode>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TaxCodeId })
                .HasPrincipalKey(tc => new { tc.TenantId, tc.LegalEntityId, tc.TaxCodeId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptSequence>(entity =>
        {
            entity.ToTable("receipt_sequences", "procurement", t =>
            {
                t.HasCheckConstraint("ck_receipt_sequences_nonnegative", "last_number >= 0");
                t.HasCheckConstraint("ck_receipt_sequences_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("goods_receipts", "procurement", t =>
            {
                t.HasCheckConstraint("ck_goods_receipts_status", "status IN ('ARRIVED', 'IN_DOCK_INSPECTION', 'RELEASED_FOR_ACCEPTANCE', 'PARTIALLY_ACCEPTED', 'VALUED_ACCEPTED', 'FULLY_REJECTED_UNVALUED')");
                t.HasCheckConstraint("ck_goods_receipts_financial_consistency", "(total_valued_accepted_base_quantity = 0 AND has_financial_posting = false) OR (total_valued_accepted_base_quantity > 0 AND has_financial_posting = true)");
                t.HasCheckConstraint("ck_goods_receipts_valued_le_released", "total_valued_accepted_base_quantity <= total_released_base_quantity");
                t.HasCheckConstraint("ck_goods_receipts_nonnegative", "total_delivered_base_quantity > 0 AND total_rejected_base_quantity >= 0 AND total_valued_accepted_base_quantity >= 0 AND total_unresolved_custody_base_quantity >= 0 AND total_released_base_quantity >= 0 AND total_held_base_quantity >= 0");
                t.HasCheckConstraint("ck_goods_receipts_conservation", "total_delivered_base_quantity = total_rejected_base_quantity + total_valued_accepted_base_quantity + total_unresolved_custody_base_quantity");
            });

            entity.HasKey(x => x.GoodsReceiptId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId });

            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoRevisionId).HasColumnName("po_revision_id").IsRequired();
            entity.Property(x => x.PoNumberSnapshot).HasColumnName("po_number_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.PoRevisionNumberSnapshot).HasColumnName("po_revision_number_snapshot").IsRequired();
            entity.Property(x => x.PoSnapshotHash).HasColumnName("po_snapshot_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierDeliveryNoteNumber).HasColumnName("supplier_delivery_note_number").HasMaxLength(80).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.ArrivalPayloadHash).HasColumnName("arrival_payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReceivedDate).HasColumnName("received_date").IsRequired();
            entity.Property(x => x.ReceivingStation).HasColumnName("receiving_station").HasMaxLength(160).IsRequired();
            entity.Property(x => x.VehicleOrSealRef).HasColumnName("vehicle_or_seal_ref").HasMaxLength(80);
            entity.Property(x => x.DocumentEvidenceRef).HasColumnName("document_evidence_ref").HasMaxLength(200);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.TotalDeliveredBaseQuantity).HasColumnName("total_delivered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalRejectedBaseQuantity).HasColumnName("total_rejected_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalValuedAcceptedBaseQuantity).HasColumnName("total_valued_accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalUnresolvedCustodyBaseQuantity).HasColumnName("total_unresolved_custody_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalReleasedBaseQuantity).HasColumnName("total_released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalHeldBaseQuantity).HasColumnName("total_held_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.HasFinancialPosting).HasColumnName("has_financial_posting").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(x => x.ReplacementAuthorizationId).HasColumnName("replacement_authorization_id");
            entity.Property(x => x.ReceivedByUserId).HasColumnName("received_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceiptNumber })
                .IsUnique()
                .HasDatabaseName("ux_goods_receipts_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_goods_receipts_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierPartnerId, x.SupplierDeliveryNoteNumber })
                .IsUnique()
                .HasDatabaseName("ux_goods_receipts_supplier_delivery_note");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.ReceivedDate });
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReplacementAuthorizationId })
                .HasDatabaseName("ix_goods_receipts_replacement_auth");

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId })
                .HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderRevision>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoRevisionId })
                .HasPrincipalKey(r => new { r.TenantId, r.LegalEntityId, r.RevisionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ReplacementAuthorization>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReplacementAuthorizationId })
                .HasPrincipalKey(ra => new { ra.TenantId, ra.LegalEntityId, ra.ReplacementAuthorizationId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ReceivedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.ToTable("goods_receipt_lines", "procurement", t =>
            {
                t.HasCheckConstraint("ck_gr_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_gr_lines_custody_status", "custody_status IN ('ARRIVED', 'DOCK_CHECK_PENDING', 'DOCK_HELD', 'DOCK_REJECTED', 'DOCK_RELEASED')");
                t.HasCheckConstraint("ck_gr_lines_positive_delivered", "physically_delivered_quantity > 0 AND physically_delivered_base_quantity > 0 AND uom_factor_to_base_snapshot > 0");
                t.HasCheckConstraint("ck_gr_lines_nonnegative_buckets", "pending_check_base_quantity >= 0 AND dock_held_base_quantity >= 0 AND dock_released_base_quantity >= 0 AND dock_rejected_base_quantity >= 0 AND valued_accepted_base_quantity >= 0 AND unresolved_custody_base_quantity >= 0 AND overdelivery_excess_base_quantity >= 0");
                t.HasCheckConstraint("ck_gr_lines_valued_le_released", "valued_accepted_base_quantity <= dock_released_base_quantity");
                t.HasCheckConstraint("ck_gr_lines_conservation", "physically_delivered_base_quantity = dock_rejected_base_quantity + valued_accepted_base_quantity + unresolved_custody_base_quantity AND unresolved_custody_base_quantity = pending_check_base_quantity + dock_held_base_quantity + (dock_released_base_quantity - valued_accepted_base_quantity)");
            });

            entity.HasKey(x => x.GoodsReceiptLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId });

            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoRevisionId).HasColumnName("po_revision_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.LineageLineKey).HasColumnName("lineage_line_key").HasMaxLength(48).IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ItemCodeSnapshot).HasColumnName("item_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ItemNameSnapshot).HasColumnName("item_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.ItemKindSnapshot).HasColumnName("item_kind_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ReceivedUomId).HasColumnName("received_uom_id").IsRequired();
            entity.Property(x => x.ReceivedUomCodeSnapshot).HasColumnName("received_uom_code_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.UomFactorToBaseSnapshot).HasColumnName("uom_factor_to_base_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PoOrderedBaseQuantitySnapshot).HasColumnName("po_ordered_base_quantity_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PhysicallyDeliveredQuantity).HasColumnName("physically_delivered_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PhysicallyDeliveredBaseQuantity).HasColumnName("physically_delivered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PendingCheckBaseQuantity).HasColumnName("pending_check_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockHeldBaseQuantity).HasColumnName("dock_held_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockReleasedBaseQuantity).HasColumnName("dock_released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockRejectedBaseQuantity).HasColumnName("dock_rejected_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ValuedAcceptedBaseQuantity).HasColumnName("valued_accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnresolvedCustodyBaseQuantity).HasColumnName("unresolved_custody_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.OverdeliveryExcessBaseQuantity).HasColumnName("overdelivery_excess_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.CustodyStatus).HasColumnName("custody_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.DiscrepancyReasonCode).HasColumnName("discrepancy_reason_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.DiscrepancyNotes).HasColumnName("discrepancy_notes").HasMaxLength(300);
            entity.Property(x => x.ConditionEvidenceRef).HasColumnName("condition_evidence_ref").HasMaxLength(200);
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId, x.LineNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.LineageLineKey });

            entity.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId })
                .HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId })
                .HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ReceivedUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DockCheck>(entity =>
        {
            entity.ToTable("dock_checks", "procurement", t =>
            {
                t.HasCheckConstraint("ck_dock_checks_status", "resulting_custody_status IN ('ARRIVED', 'DOCK_CHECK_PENDING', 'DOCK_HELD', 'DOCK_REJECTED', 'DOCK_RELEASED')");
                t.HasCheckConstraint("ck_dock_checks_nonnegative", "pending_check_base_quantity >= 0 AND dock_held_base_quantity >= 0 AND dock_released_base_quantity >= 0 AND dock_rejected_base_quantity >= 0");
            });

            entity.HasKey(x => x.DockCheckId);
            entity.Property(x => x.DockCheckId).HasColumnName("dock_check_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PreviousCustodyStatus).HasColumnName("previous_custody_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.ResultingCustodyStatus).HasColumnName("resulting_custody_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PendingCheckBaseQuantity).HasColumnName("pending_check_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockHeldBaseQuantity).HasColumnName("dock_held_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockReleasedBaseQuantity).HasColumnName("dock_released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DockRejectedBaseQuantity).HasColumnName("dock_rejected_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DiscrepancyReasonCode).HasColumnName("discrepancy_reason_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.InspectionNotes).HasColumnName("inspection_notes").HasMaxLength(300);
            entity.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(200);
            entity.Property(x => x.CheckedByUserId).HasColumnName("checked_by_user_id").IsRequired();
            entity.Property(x => x.CheckedAtUtc).HasColumnName("checked_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_dock_checks_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId, x.CheckedAtUtc });

            entity.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId })
                .HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceiptLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.GoodsReceiptLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CheckedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AcceptanceSequence>(entity =>
        {
            entity.ToTable("acceptance_sequences", "procurement", t =>
            {
                t.HasCheckConstraint("ck_acceptance_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                t.HasCheckConstraint("ck_acceptance_seq_nonneg", "last_number >= 0");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ValuedAcceptance>(entity =>
        {
            entity.ToTable("valued_acceptances", "procurement", t =>
            {
                t.HasCheckConstraint("ck_valued_acceptances_stock_status", "stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                t.HasCheckConstraint("ck_valued_acceptances_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_valued_acceptances_positive", "total_accepted_base_quantity > 0 AND total_accepted_amount > 0");
                t.HasCheckConstraint("ck_valued_acceptances_links", "stock_movement_id IS NOT NULL AND receipt_accrual_id IS NOT NULL AND gl_journal_id IS NOT NULL");
            });

            entity.HasKey(x => x.ValuedAcceptanceId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId });

            entity.Property(x => x.ValuedAcceptanceId).HasColumnName("valued_acceptance_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.AcceptanceNumber).HasColumnName("acceptance_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoRevisionId).HasColumnName("po_revision_id").IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.AcceptanceDate).HasColumnName("acceptance_date").IsRequired();
            entity.Property(x => x.StockStatus).HasColumnName("stock_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.TotalAcceptedBaseQuantity).HasColumnName("total_accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalAcceptedAmount).HasColumnName("total_accepted_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id");
            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id");
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id");
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(400);
            entity.Property(x => x.AcceptedByUserId).HasColumnName("accepted_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AcceptanceNumber })
                .IsUnique()
                .HasDatabaseName("ux_valued_acceptances_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_valued_acceptances_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId, x.AcceptanceDate })
                .HasDatabaseName("ix_valued_acceptances_gr_date");

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId })
                .HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId })
                .HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderRevision>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoRevisionId })
                .HasPrincipalKey(r => new { r.TenantId, r.LegalEntityId, r.RevisionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.AcceptedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ValuedAcceptanceLine>(entity =>
        {
            entity.ToTable("valued_acceptance_lines", "procurement", t =>
            {
                t.HasCheckConstraint("ck_val_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_val_lines_positive", "accepted_base_quantity > 0 AND unit_price_base_snapshot > 0 AND accepted_line_amount > 0");
            });

            entity.HasKey(x => x.ValuedAcceptanceLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId });

            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ValuedAcceptanceId).HasColumnName("valued_acceptance_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(16).IsRequired();
            entity.Property(x => x.AcceptedBaseQuantity).HasColumnName("accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnitPriceBaseSnapshot).HasColumnName("unit_price_base_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AcceptedLineAmount).HasColumnName("accepted_line_amount").HasColumnType("numeric(18,4)").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId, x.LineNumber })
                .IsUnique()
                .HasDatabaseName("ux_val_lines_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId, x.GoodsReceiptLineId })
                .IsUnique()
                .HasDatabaseName("ux_val_lines_acceptance_grline");

            entity.HasOne<ValuedAcceptance>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId })
                .HasPrincipalKey(va => new { va.TenantId, va.LegalEntityId, va.ValuedAcceptanceId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceiptLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.GoodsReceiptLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId })
                .HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MovementSequence>(entity =>
        {
            entity.ToTable("movement_sequences", "inventory", t =>
            {
                t.HasCheckConstraint("ck_movement_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                t.HasCheckConstraint("ck_movement_seq_nonneg", "last_number >= 0");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockPosition>(entity =>
        {
            entity.ToTable("stock_positions", "inventory", t =>
            {
                t.HasCheckConstraint("ck_stock_positions_nonnegative", "unrestricted_base_quantity >= 0 AND quality_inspection_base_quantity >= 0 AND blocked_base_quantity >= 0 AND reserved_base_quantity >= 0 AND total_valued_on_hand_base_quantity >= 0 AND available_base_quantity >= 0");
                t.HasCheckConstraint("ck_stock_positions_balance", "total_valued_on_hand_base_quantity = unrestricted_base_quantity + quality_inspection_base_quantity + blocked_base_quantity AND available_base_quantity = unrestricted_base_quantity - reserved_base_quantity");
            });

            entity.HasKey(x => x.StockPositionId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.StockPositionId });

            entity.Property(x => x.StockPositionId).HasColumnName("stock_position_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ValuationGroupCode).HasColumnName("valuation_group_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.UnrestrictedBaseQuantity).HasColumnName("unrestricted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.QualityInspectionBaseQuantity).HasColumnName("quality_inspection_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.BlockedBaseQuantity).HasColumnName("blocked_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReservedBaseQuantity).HasColumnName("reserved_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalValuedOnHandBaseQuantity).HasColumnName("total_valued_on_hand_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AvailableBaseQuantity).HasColumnName("available_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId, x.ValuationGroupCode })
                .IsUnique()
                .HasDatabaseName("ux_stock_positions_item_group");

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

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ValuationPool>(entity =>
        {
            entity.ToTable("valuation_pools", "inventory", t =>
            {
                t.HasCheckConstraint("ck_valuation_pools_status", "status IN ('ACTIVE', 'BLOCKED')");
                t.HasCheckConstraint("ck_valuation_pools_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_valuation_pools_od04", "valued_base_quantity >= 0 AND carrying_value_amount >= 0 AND moving_average_unit_cost >= 0 AND last_entry_sequence >= 0 AND (valued_base_quantity > 0 OR carrying_value_amount = 0)");
            });

            entity.HasKey(x => x.ValuationPoolId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ValuationPoolId });

            entity.Property(x => x.ValuationPoolId).HasColumnName("valuation_pool_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.ValuationGroupCode).HasColumnName("valuation_group_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.ValuedBaseQuantity).HasColumnName("valued_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.CarryingValueAmount).HasColumnName("carrying_value_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.MovingAverageUnitCost).HasColumnName("moving_average_unit_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.LastEntrySequence).HasColumnName("last_entry_sequence").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId, x.CurrencyCode, x.ValuationGroupCode })
                .IsUnique()
                .HasDatabaseName("ux_valuation_pools_item_group");

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

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements", "inventory", t =>
            {
                t.HasCheckConstraint("ck_stock_movements_type", "movement_type IN ('PURCHASE_RECEIPT_ACCEPTANCE', 'LOCATION_TRANSFER', 'QUALITY_STATUS_CHANGE', 'OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND')");
            });

            entity.HasKey(x => x.StockMovementId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId });

            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.MovementNumber).HasColumnName("movement_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.SourceDocumentNumber).HasColumnName("source_document_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MovementNumber })
                .IsUnique()
                .HasDatabaseName("ux_stock_movements_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceDocumentType, x.SourceDocumentId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_stock_movements_source_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PostingDate })
                .HasDatabaseName("ix_stock_movements_posting_date");

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MovementLine>(entity =>
        {
            entity.ToTable("movement_lines", "inventory", t =>
            {
                t.HasCheckConstraint("ck_movement_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_movement_lines_status", "resulting_stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                t.HasCheckConstraint("ck_movement_lines_positive", "base_quantity_delta <> 0 AND unit_valuation_cost >= 0 AND ((base_quantity_delta > 0 AND line_valuation_amount >= 0) OR (base_quantity_delta < 0 AND line_valuation_amount <= 0))");
            });

            entity.HasKey(x => x.MovementLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.MovementLineId });

            entity.Property(x => x.MovementLineId).HasColumnName("movement_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.StockPositionId).HasColumnName("stock_position_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.SourceLineId).HasColumnName("source_line_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(x => x.LocationId).HasColumnName("location_id");
            entity.Property(x => x.StockLotId).HasColumnName("stock_lot_id");
            entity.Property(x => x.ResultingStockStatus).HasColumnName("resulting_stock_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.BaseQuantityDelta).HasColumnName("base_quantity_delta").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnitValuationCost).HasColumnName("unit_valuation_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.LineValuationAmount).HasColumnName("line_valuation_amount").HasColumnType("numeric(18,4)").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId, x.LineNumber })
                .IsUnique()
                .HasDatabaseName("ux_movement_lines_number");

            entity.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId })
                .HasPrincipalKey(m => new { m.TenantId, m.LegalEntityId, m.StockMovementId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockPosition>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockPositionId })
                .HasPrincipalKey(sp => new { sp.TenantId, sp.LegalEntityId, sp.StockPositionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId })
                .HasPrincipalKey(w => new { w.TenantId, w.LegalEntityId, w.WarehouseId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WarehouseLocation>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.LocationId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.LocationId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockLot>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockLotId })
                .HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.StockLotId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ValuationEntry>(entity =>
        {
            entity.ToTable("valuation_entries", "inventory", t =>
            {
                t.HasCheckConstraint("ck_valuation_entries_type", "entry_type IN ('RECEIPT_ACCEPTANCE', 'OPERATIONAL_ISSUE', 'INVOICE_PRICE_ADJUSTMENT', 'PURCHASE_RETURN_OUTBOUND')");
                t.HasCheckConstraint("ck_valuation_entries_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_valuation_entries_seq", "pool_sequence_number >= 1");
                t.HasCheckConstraint("ck_valuation_entries_values", "previous_pool_base_quantity >= 0 AND previous_pool_carrying_value >= 0 AND previous_moving_average_cost >= 0 AND new_pool_base_quantity >= 0 AND new_pool_carrying_value >= 0 AND new_moving_average_cost >= 0 AND ((entry_type = 'RECEIPT_ACCEPTANCE' AND quantity_delta > 0 AND value_delta > 0 AND unit_cost_applied > 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type IN ('OPERATIONAL_ISSUE', 'PURCHASE_RETURN_OUTBOUND') AND quantity_delta < 0 AND value_delta < 0 AND unit_cost_applied > 0 AND stock_movement_id IS NOT NULL AND movement_line_id IS NOT NULL AND gl_journal_id IS NOT NULL) OR (entry_type = 'INVOICE_PRICE_ADJUSTMENT' AND quantity_delta = 0 AND value_delta <> 0 AND new_pool_base_quantity > 0 AND new_pool_carrying_value > 0 AND new_moving_average_cost > 0 AND gl_journal_id IS NOT NULL))");
            });

            entity.HasKey(x => x.ValuationEntryId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ValuationEntryId });
            entity.Property(x => x.ValuationEntryId).HasColumnName("valuation_entry_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ValuationPoolId).HasColumnName("valuation_pool_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.PoolSequenceNumber).HasColumnName("pool_sequence_number").IsRequired();
            entity.Property(x => x.EntryType).HasColumnName("entry_type").HasMaxLength(36).IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id");
            entity.Property(x => x.MovementLineId).HasColumnName("movement_line_id");
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.SourceDocumentNumber).HasColumnName("source_document_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.QuantityDelta).HasColumnName("quantity_delta").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ValueDelta).HasColumnName("value_delta").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.UnitCostApplied).HasColumnName("unit_cost_applied").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PreviousPoolBaseQuantity).HasColumnName("previous_pool_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PreviousPoolCarryingValue).HasColumnName("previous_pool_carrying_value").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PreviousMovingAverageCost).HasColumnName("previous_moving_average_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.NewPoolBaseQuantity).HasColumnName("new_pool_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.NewPoolCarryingValue).HasColumnName("new_pool_carrying_value").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.NewMovingAverageCost).HasColumnName("new_moving_average_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ValuationPoolId, x.PoolSequenceNumber })
                .IsUnique()
                .HasDatabaseName("ux_valuation_entries_pool_seq");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId, x.PostingDate })
                .HasDatabaseName("ix_valuation_entries_item_date");

            entity.HasOne<ValuationPool>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuationPoolId })
                .HasPrincipalKey(vp => new { vp.TenantId, vp.LegalEntityId, vp.ValuationPoolId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId })
                .HasPrincipalKey(m => new { m.TenantId, m.LegalEntityId, m.StockMovementId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<MovementLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MovementLineId })
                .HasPrincipalKey(ml => new { ml.TenantId, ml.LegalEntityId, ml.MovementLineId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId })
                .HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptAccrual>(entity =>
        {
            entity.ToTable("receipt_accruals", "gl", t =>
            {
                t.HasCheckConstraint("ck_receipt_accruals_status", "status IN ('OPEN', 'PARTIALLY_CLEARED', 'CLEARED')");
                t.HasCheckConstraint("ck_receipt_accruals_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_receipt_accruals_amounts", "accrued_amount > 0 AND cleared_amount >= 0 AND returned_amount >= 0 AND remaining_open_amount >= 0 AND accrued_amount = cleared_amount + returned_amount + remaining_open_amount");
            });

            entity.HasKey(x => x.ReceiptAccrualId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId });

            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.AccrualNumber).HasColumnName("accrual_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ValuedAcceptanceId).HasColumnName("valued_acceptance_id").IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.JournalId).HasColumnName("journal_id").IsRequired();
            entity.Property(x => x.InventoryAssetAccountId).HasColumnName("inventory_asset_account_id").IsRequired();
            entity.Property(x => x.GrniAccountId).HasColumnName("grni_account_id").IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.AccruedAmount).HasColumnName("accrued_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ClearedAmount).HasColumnName("cleared_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ReturnedAmount).HasColumnName("returned_amount").HasColumnType("numeric(18,4)").HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.RemainingOpenAmount).HasColumnName("remaining_open_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AccrualNumber })
                .IsUnique()
                .HasDatabaseName("ux_receipt_accruals_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId })
                .IsUnique()
                .HasDatabaseName("ux_receipt_accruals_acceptance");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierPartnerId, x.Status, x.PostingDate })
                .HasDatabaseName("ix_receipt_accruals_supplier_status");

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ValuedAcceptance>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId })
                .HasPrincipalKey(va => new { va.TenantId, va.LegalEntityId, va.ValuedAcceptanceId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId })
                .HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId })
                .HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Partner>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId })
                .HasPrincipalKey(p => new { p.TenantId, p.PartnerId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.JournalId })
                .HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.InventoryAssetAccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GlAccount>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrniAccountId })
                .HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptAccrualLine>(entity =>
        {
            entity.ToTable("receipt_accrual_lines", "gl", t =>
            {
                t.HasCheckConstraint("ck_receipt_accrual_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_receipt_accrual_lines_qty", "accepted_base_quantity > 0 AND billed_base_quantity >= 0 AND returned_base_quantity >= 0 AND remaining_unbilled_base_quantity >= 0 AND accepted_base_quantity = billed_base_quantity + returned_base_quantity + remaining_unbilled_base_quantity");
                t.HasCheckConstraint("ck_receipt_accrual_lines_amt", "unit_cost_base > 0 AND accrued_line_amount > 0 AND cleared_line_amount >= 0 AND returned_line_amount >= 0 AND remaining_open_line_amount >= 0 AND accrued_line_amount = cleared_line_amount + returned_line_amount + remaining_open_line_amount");
            });

            entity.HasKey(x => x.ReceiptAccrualLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualLineId });

            entity.Property(x => x.ReceiptAccrualLineId).HasColumnName("receipt_accrual_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id").IsRequired();
            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.AcceptedBaseQuantity).HasColumnName("accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.BilledBaseQuantity).HasColumnName("billed_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReturnedBaseQuantity).HasColumnName("returned_base_quantity").HasColumnType("numeric(18,6)").HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.RemainingUnbilledBaseQuantity).HasColumnName("remaining_unbilled_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnitCostBase).HasColumnName("unit_cost_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AccruedLineAmount).HasColumnName("accrued_line_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ClearedLineAmount).HasColumnName("cleared_line_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ReturnedLineAmount).HasColumnName("returned_line_amount").HasColumnType("numeric(18,4)").HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.RemainingOpenLineAmount).HasColumnName("remaining_open_line_amount").HasColumnType("numeric(18,4)").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId, x.LineNumber })
                .IsUnique()
                .HasDatabaseName("ux_receipt_accrual_lines_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId, x.ValuedAcceptanceLineId })
                .IsUnique()
                .HasDatabaseName("ux_receipt_accrual_lines_accrual_vline");

            entity.HasOne<ReceiptAccrual>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId })
                .HasPrincipalKey(ra => new { ra.TenantId, ra.LegalEntityId, ra.ReceiptAccrualId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ValuedAcceptanceLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId })
                .HasPrincipalKey(vl => new { vl.TenantId, vl.LegalEntityId, vl.ValuedAcceptanceLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceiptLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.GoodsReceiptLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PurchaseOrderLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId })
                .HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses", "inventory", t =>
            {
                t.HasCheckConstraint("ck_warehouses_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.WarehouseId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId });

            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.WarehouseCode).HasColumnName("warehouse_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.WarehouseCode })
                .IsUnique()
                .HasDatabaseName("ux_warehouses_code");

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WarehouseLocation>(entity =>
        {
            entity.ToTable("locations", "inventory", t =>
            {
                t.HasCheckConstraint("ck_locations_zone", "zone_type IN ('RECEIVING_STAGING', 'STORAGE', 'QUARANTINE', 'BLOCKED_HOLD', 'DISPATCH')");
                t.HasCheckConstraint("ck_locations_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.LocationId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.LocationId });

            entity.Property(x => x.LocationId).HasColumnName("location_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.LocationCode).HasColumnName("location_code").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.ZoneType).HasColumnName("zone_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsDefaultReceiving).HasColumnName("is_default_receiving").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.LocationCode })
                .IsUnique()
                .HasDatabaseName("ux_locations_code");

            entity.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId })
                .HasPrincipalKey(w => new { w.TenantId, w.LegalEntityId, w.WarehouseId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockLot>(entity =>
        {
            entity.ToTable("stock_lots", "inventory", t =>
            {
                t.HasCheckConstraint("ck_stock_lots_dates", "manufactured_date IS NULL OR expiry_date IS NULL OR expiry_date >= manufactured_date");
            });

            entity.HasKey(x => x.StockLotId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.StockLotId });

            entity.Property(x => x.StockLotId).HasColumnName("stock_lot_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.LotNumber).HasColumnName("lot_number").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SupplierLotReference).HasColumnName("supplier_lot_reference").HasMaxLength(64);
            entity.Property(x => x.ManufacturedDate).HasColumnName("manufactured_date");
            entity.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(x => x.SourceValuedAcceptanceId).HasColumnName("source_valued_acceptance_id");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId, x.LotNumber })
                .IsUnique()
                .HasDatabaseName("ux_stock_lots_number");

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

            entity.HasOne<ValuedAcceptance>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceValuedAcceptanceId })
                .HasPrincipalKey(v => new { v.TenantId, v.LegalEntityId, v.ValuedAcceptanceId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockLocationBalance>(entity =>
        {
            entity.ToTable("stock_location_balances", "inventory", t =>
            {
                t.HasCheckConstraint("ck_loc_bal_status", "stock_status IN ('UNRESTRICTED', 'QUALITY_INSPECTION', 'BLOCKED')");
                t.HasCheckConstraint("ck_loc_bal_nonnegative", "on_hand_base_quantity >= 0 AND reserved_base_quantity >= 0 AND available_base_quantity >= 0");
                t.HasCheckConstraint("ck_loc_bal_availability", "(stock_status = 'UNRESTRICTED' AND available_base_quantity = on_hand_base_quantity - reserved_base_quantity) OR (stock_status IN ('QUALITY_INSPECTION', 'BLOCKED') AND reserved_base_quantity = 0 AND available_base_quantity = 0)");
            });

            entity.HasKey(x => x.StockLocationBalanceId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.StockLocationBalanceId });

            entity.Property(x => x.StockLocationBalanceId).HasColumnName("stock_location_balance_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.StockPositionId).HasColumnName("stock_position_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
            entity.Property(x => x.StockLotId).HasColumnName("stock_lot_id");
            entity.Property(x => x.StockStatus).HasColumnName("stock_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.OnHandBaseQuantity).HasColumnName("on_hand_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReservedBaseQuantity).HasColumnName("reserved_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AvailableBaseQuantity).HasColumnName("available_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId, x.LocationId, x.ItemId, x.StockStatus })
                .IsUnique()
                .HasFilter("stock_lot_id IS NULL")
                .HasDatabaseName("ux_loc_bal_unlotted");

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId, x.LocationId, x.ItemId, x.StockLotId, x.StockStatus })
                .IsUnique()
                .HasFilter("stock_lot_id IS NOT NULL")
                .HasDatabaseName("ux_loc_bal_lotted");

            entity.HasOne<StockPosition>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockPositionId })
                .HasPrincipalKey(sp => new { sp.TenantId, sp.LegalEntityId, sp.StockPositionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UnitOfMeasure>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.BaseUomId })
                .HasPrincipalKey(u => new { u.TenantId, u.UomId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Warehouse>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.WarehouseId })
                .HasPrincipalKey(w => new { w.TenantId, w.LegalEntityId, w.WarehouseId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WarehouseLocation>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.LocationId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.LocationId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockLot>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockLotId })
                .HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.StockLotId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReservationSequence>(entity =>
        {
            entity.ToTable("reservation_sequences", "inventory", t =>
            {
                t.HasCheckConstraint("ck_reservation_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                t.HasCheckConstraint("ck_reservation_seq_nonneg", "last_number >= 0");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("stock_reservations", "inventory", t =>
            {
                t.HasCheckConstraint("ck_stock_reservations_source_type", "source_type IN ('SALES_ORDER', 'MANUAL_HOLD', 'FULFILLMENT')");
                t.HasCheckConstraint("ck_stock_reservations_status", "status IN ('ACTIVE', 'PARTIALLY_CONSUMED', 'FULFILLED', 'RELEASED')");
                t.HasCheckConstraint("ck_stock_reservations_qty", "reserved_base_quantity > 0 AND consumed_base_quantity >= 0 AND released_base_quantity >= 0 AND remaining_reserved_base_quantity >= 0 AND reserved_base_quantity = consumed_base_quantity + released_base_quantity + remaining_reserved_base_quantity");
            });

            entity.HasKey(x => x.StockReservationId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.StockReservationId });

            entity.Property(x => x.StockReservationId).HasColumnName("stock_reservation_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ReservationNumber).HasColumnName("reservation_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.StockPositionId).HasColumnName("stock_position_id").IsRequired();
            entity.Property(x => x.StockLocationBalanceId).HasColumnName("stock_location_balance_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.LocationId).HasColumnName("location_id").IsRequired();
            entity.Property(x => x.StockLotId).HasColumnName("stock_lot_id");
            entity.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id");
            entity.Property(x => x.SourceDocumentNumber).HasColumnName("source_document_number").HasMaxLength(64).IsRequired();
            entity.Property(x => x.SourceLineId).HasColumnName("source_line_id");
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReservedBaseQuantity).HasColumnName("reserved_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ConsumedBaseQuantity).HasColumnName("consumed_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReleasedBaseQuantity).HasColumnName("released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.RemainingReservedBaseQuantity).HasColumnName("remaining_reserved_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.ReleasedByUserId).HasColumnName("released_by_user_id");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReservationNumber })
                .IsUnique()
                .HasDatabaseName("ux_stock_reservations_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_stock_reservations_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ItemId, x.Status })
                .HasDatabaseName("ix_stock_reservations_item_status");

            entity.HasOne<StockPosition>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockPositionId })
                .HasPrincipalKey(sp => new { sp.TenantId, sp.LegalEntityId, sp.StockPositionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockLocationBalance>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockLocationBalanceId })
                .HasPrincipalKey(lb => new { lb.TenantId, lb.LegalEntityId, lb.StockLocationBalanceId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WarehouseLocation>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.LocationId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.LocationId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockLot>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockLotId })
                .HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.StockLotId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.CreatedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InspectionSequence>(entity =>
        {
            entity.ToTable("inspection_sequences", "quality", t =>
            {
                t.HasCheckConstraint("ck_inspection_seq_year", "fiscal_year >= 2000 AND fiscal_year <= 2100");
                t.HasCheckConstraint("ck_inspection_seq_nonneg", "last_number >= 0");
            });

            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();

            entity.HasOne<LegalEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptInspection>(entity =>
        {
            entity.ToTable("receipt_inspections", "quality", t =>
            {
                t.HasCheckConstraint("ck_receipt_inspections_status", "status IN ('PENDING', 'PARTIALLY_DECIDED', 'COMPLETED')");
                t.HasCheckConstraint("ck_receipt_inspections_qty", "inspected_total_base_quantity > 0 AND pending_base_quantity >= 0 AND released_base_quantity >= 0 AND blocked_base_quantity >= 0 AND inspected_total_base_quantity = pending_base_quantity + released_base_quantity + blocked_base_quantity");
            });

            entity.HasKey(x => x.ReceiptInspectionId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptInspectionId });

            entity.Property(x => x.ReceiptInspectionId).HasColumnName("receipt_inspection_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.InspectionNumber).HasColumnName("inspection_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ValuedAcceptanceId).HasColumnName("valued_acceptance_id").IsRequired();
            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id").IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.QuarantineLocationId).HasColumnName("quarantine_location_id").IsRequired();
            entity.Property(x => x.StockLotId).HasColumnName("stock_lot_id");
            entity.Property(x => x.InspectedTotalBaseQuantity).HasColumnName("inspected_total_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PendingBaseQuantity).HasColumnName("pending_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReleasedBaseQuantity).HasColumnName("released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.BlockedBaseQuantity).HasColumnName("blocked_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.InspectionNumber })
                .IsUnique()
                .HasDatabaseName("ux_receipt_inspections_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId })
                .IsUnique()
                .HasDatabaseName("ux_receipt_inspections_acceptance_line");

            entity.HasOne<ValuedAcceptance>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceId })
                .HasPrincipalKey(v => new { v.TenantId, v.LegalEntityId, v.ValuedAcceptanceId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ValuedAcceptanceLine>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId })
                .HasPrincipalKey(vl => new { vl.TenantId, vl.LegalEntityId, vl.ValuedAcceptanceLineId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<GoodsReceipt>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId })
                .HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.ItemId })
                .HasPrincipalKey(i => new { i.TenantId, i.ItemId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<WarehouseLocation>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.QuarantineLocationId })
                .HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.LocationId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockLot>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockLotId })
                .HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.StockLotId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InspectionDisposition>(entity =>
        {
            entity.ToTable("inspection_dispositions", "quality", t =>
            {
                t.HasCheckConstraint("ck_inspection_disp_qty", "released_base_quantity >= 0 AND blocked_base_quantity >= 0 AND (released_base_quantity + blocked_base_quantity) > 0");
                t.HasCheckConstraint("ck_inspection_disp_reason", "reason_code IN ('PASSED_SPECIFICATION', 'DAMAGED_OR_CONTAMINATED', 'SPEC_DEVIATION_HOLD', 'MISSING_CERTIFICATE', 'SUPERVISOR_REEVALUATION')");
                t.HasCheckConstraint("ck_inspection_disp_targets", "(released_base_quantity = 0 OR target_release_location_id IS NOT NULL) AND (blocked_base_quantity = 0 OR target_block_location_id IS NOT NULL)");
            });

            entity.HasKey(x => x.InspectionDispositionId);
            entity.Property(x => x.InspectionDispositionId).HasColumnName("inspection_disposition_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ReceiptInspectionId).HasColumnName("receipt_inspection_id").IsRequired();
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id").IsRequired();
            entity.Property(x => x.DispositionDate).HasColumnName("disposition_date").IsRequired();
            entity.Property(x => x.ReleasedBaseQuantity).HasColumnName("released_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TargetReleaseLocationId).HasColumnName("target_release_location_id");
            entity.Property(x => x.BlockedBaseQuantity).HasColumnName("blocked_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TargetBlockLocationId).HasColumnName("target_block_location_id");
            entity.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(120);
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(400);
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
            entity.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id").IsRequired();
            entity.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceiptInspectionId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("ux_inspection_dispositions_idempotency");

            entity.HasOne<ReceiptInspection>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptInspectionId })
                .HasPrincipalKey(q => new { q.TenantId, q.LegalEntityId, q.ReceiptInspectionId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<StockMovement>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId })
                .HasPrincipalKey(m => new { m.TenantId, m.LegalEntityId, m.StockMovementId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<TenantMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.DecidedByUserId })
                .HasPrincipalKey(m => new { m.TenantId, m.UserId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BillSequence>(entity =>
        {
            entity.ToTable("bill_sequences", "ap");
            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayableSequence>(entity =>
        {
            entity.ToTable("payable_sequences", "ap");
            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceMatchPolicy>(entity =>
        {
            entity.ToTable("invoice_match_policies", "ap", t =>
            {
                t.HasCheckConstraint("ck_inv_match_policies_review", "review_status IN ('UNREVIEWED_SAFE_DEFAULT', 'REVIEWED_INTERNAL')");
                t.HasCheckConstraint("ck_inv_match_policies_limits", "pp_tolerance_percent >= 0 AND pp_tolerance_amount >= 0 AND dq_tolerance_percent >= 0 AND dq_tolerance_base_qty >= 0 AND bd_write_off_limit_amount >= 0");
                t.HasCheckConstraint("ck_inv_match_policies_status", "status IN ('ACTIVE', 'INACTIVE')");
            });

            entity.HasKey(x => x.PolicyId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyId });
            entity.Property(x => x.PolicyId).HasColumnName("policy_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(48).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PpTolerancePercent).HasColumnName("pp_tolerance_percent").HasColumnType("numeric(9,4)").IsRequired();
            entity.Property(x => x.PpToleranceAmount).HasColumnName("pp_tolerance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.DqTolerancePercent).HasColumnName("dq_tolerance_percent").HasColumnType("numeric(9,4)").IsRequired();
            entity.Property(x => x.DqToleranceBaseQty).HasColumnName("dq_tolerance_base_qty").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.BdWriteOffLimitAmount).HasColumnName("bd_write_off_limit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.BdWriteOffAccountId).HasColumnName("bd_write_off_account_id");
            entity.Property(x => x.IsDefault).HasColumnName("is_default").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Code }).IsUnique().HasDatabaseName("ux_invoice_match_policies_code");
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GlAccount>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.BdWriteOffAccountId }).HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.AccountId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierBill>(entity =>
        {
            entity.ToTable("supplier_bills", "ap", t =>
            {
                t.HasCheckConstraint("ck_supplier_bills_type", "bill_type IN ('PO_THREE_WAY_MATCH')");
                t.HasCheckConstraint("ck_supplier_bills_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'POSTED', 'CANCELLED')");
                t.HasCheckConstraint("ck_supplier_bills_match_status", "match_status IN ('MATCHED', 'WITHIN_TOLERANCE', 'APPROVAL_REQUIRED', 'POSTING_BLOCKED')");
                t.HasCheckConstraint("ck_supplier_bills_pay_block", "payment_block_status IN ('NONE', 'PAYMENT_BLOCKED', 'RELEASED')");
                t.HasCheckConstraint("ck_supplier_bills_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_supplier_bills_amounts", "declared_gross_amount > 0 AND lines_net_subtotal >= 0 AND total_grni_clear_amount >= 0 AND ppn_tax_total >= 0 AND withholding_tax_total >= 0 AND computed_gross_total >= 0 AND net_payable_amount >= 0 AND due_date >= invoice_date");
            });

            entity.HasKey(x => x.SupplierBillId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId });
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.DraftCode).HasColumnName("draft_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.BillNumber).HasColumnName("bill_number").HasMaxLength(40);
            entity.Property(x => x.BillType).HasColumnName("bill_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PurchaseOrderRevisionId).HasColumnName("purchase_order_revision_id").IsRequired();
            entity.Property(x => x.PoNumberSnapshot).HasColumnName("po_number_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierNpwpSnapshot).HasColumnName("supplier_npwp_snapshot").HasMaxLength(64);
            entity.Property(x => x.SupplierInvoiceNumber).HasColumnName("supplier_invoice_number").HasMaxLength(64).IsRequired();
            entity.Property(x => x.TaxInvoiceNumber).HasColumnName("tax_invoice_number").HasMaxLength(80);
            entity.Property(x => x.WithholdingSlipReference).HasColumnName("withholding_slip_reference").HasMaxLength(80);
            entity.Property(x => x.InvoiceDate).HasColumnName("invoice_date").IsRequired();
            entity.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.DeclaredGrossAmount).HasColumnName("declared_gross_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.LinesNetSubtotal).HasColumnName("lines_net_subtotal").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalGrniClearAmount).HasColumnName("total_grni_clear_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalPriceVarianceAmount).HasColumnName("total_price_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalInventoryAdjAmount).HasColumnName("total_inventory_adj_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalPnlVarianceAmount).HasColumnName("total_pnl_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalBdWriteOffAmount).HasColumnName("total_bd_write_off_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnTaxTotal).HasColumnName("ppn_tax_total").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.WithholdingTaxTotal).HasColumnName("withholding_tax_total").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ComputedGrossTotal).HasColumnName("computed_gross_total").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.NetPayableAmount).HasColumnName("net_payable_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.MatchStatus).HasColumnName("match_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PaymentBlockStatus).HasColumnName("payment_block_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PaymentBlockReason).HasColumnName("payment_block_reason").HasMaxLength(300);
            entity.Property(x => x.ApprovalPolicyCode).HasColumnName("approval_policy_code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.LastApprovalDecision).HasColumnName("last_approval_decision").HasMaxLength(16);
            entity.Property(x => x.LastRejectionReason).HasColumnName("last_rejection_reason").HasMaxLength(500);
            entity.Property(x => x.SubmittedByUserId).HasColumnName("submitted_by_user_id");
            entity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc");
            entity.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(x => x.ApprovedAtUtc).HasColumnName("approved_at_utc");
            entity.Property(x => x.VarianceApproved).HasColumnName("variance_approved").IsRequired();
            entity.Property(x => x.PostedByUserId).HasColumnName("posted_by_user_id");
            entity.Property(x => x.PostedAtUtc).HasColumnName("posted_at_utc");
            entity.Property(x => x.PostedJournalId).HasColumnName("posted_journal_id");
            entity.Property(x => x.PostIdempotencyKey).HasColumnName("post_idempotency_key").HasMaxLength(80);
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(300);
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DraftCode }).IsUnique().HasDatabaseName("ux_supplier_bills_draft_code");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.BillNumber }).IsUnique().HasFilter("bill_number IS NOT NULL").HasDatabaseName("ux_supplier_bills_bill_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierPartnerId, x.SupplierInvoiceNumber }).IsUnique().HasFilter("status <> 'CANCELLED'").HasDatabaseName("ux_supplier_bills_vendor_invoice");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.InvoiceDate }).HasDatabaseName("ix_supplier_bills_status_date");

            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId }).HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrderRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderRevisionId }).HasPrincipalKey(r => new { r.TenantId, r.LegalEntityId, r.RevisionId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Partner>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId }).HasPrincipalKey(p => new { p.TenantId, p.PartnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PostedJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierBillLine>(entity =>
        {
            entity.ToTable("supplier_bill_lines", "ap", t =>
            {
                t.HasCheckConstraint("ck_supplier_bill_lines_num", "line_number >= 1");
                t.HasCheckConstraint("ck_supplier_bill_lines_values", "billed_base_quantity > 0 AND invoice_unit_price_base > 0 AND line_net_amount > 0 AND grni_clear_amount >= 0 AND ppn_tax_amount >= 0 AND withholding_tax_amount >= 0");
                t.HasCheckConstraint("ck_supplier_bill_lines_outcomes", "dq_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND pp_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED')");
            });

            entity.HasKey(x => x.SupplierBillLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId });
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.ReceiptAccrualLineId).HasColumnName("receipt_accrual_line_id").IsRequired();
            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id").IsRequired();
            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ItemCodeSnapshot).HasColumnName("item_code_snapshot").HasMaxLength(48).IsRequired();
            entity.Property(x => x.ItemNameSnapshot).HasColumnName("item_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(20).IsRequired();
            entity.Property(x => x.OrderedBaseQtySnapshot).HasColumnName("ordered_base_qty_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AcceptedUnbilledQtySnapshot).HasColumnName("accepted_unbilled_qty_snapshot").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.BilledBaseQuantity).HasColumnName("billed_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PoUnitPriceFunctional).HasColumnName("po_unit_price_functional").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.InvoiceUnitPriceBase).HasColumnName("invoice_unit_price_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.LineNetAmount).HasColumnName("line_net_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.GrniClearAmount).HasColumnName("grni_clear_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PriceVarianceAmount).HasColumnName("price_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnTaxCodeId).HasColumnName("ppn_tax_code_id").IsRequired();
            entity.Property(x => x.PpnTaxCodeSnapshot).HasColumnName("ppn_tax_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PpnTaxAmount).HasColumnName("ppn_tax_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.WithholdingTaxCodeId).HasColumnName("withholding_tax_code_id");
            entity.Property(x => x.WithholdingTaxCodeSnapshot).HasColumnName("withholding_tax_code_snapshot").HasMaxLength(32);
            entity.Property(x => x.WithholdingTaxAmount).HasColumnName("withholding_tax_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.DqOutcome).HasColumnName("dq_outcome").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PpOutcome).HasColumnName("pp_outcome").HasMaxLength(32).IsRequired();
            entity.Property(x => x.LineNotes).HasColumnName("line_notes").HasMaxLength(240);

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId, x.LineNumber }).IsUnique().HasDatabaseName("ux_supplier_bill_lines_num");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId, x.ReceiptAccrualLineId }).IsUnique().HasDatabaseName("ux_supplier_bill_lines_accrual");

            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrualLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualLineId }).HasPrincipalKey(rl => new { rl.TenantId, rl.LegalEntityId, rl.ReceiptAccrualLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ValuedAcceptanceLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId }).HasPrincipalKey(vl => new { vl.TenantId, vl.LegalEntityId, vl.ValuedAcceptanceLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId }).HasPrincipalKey(gl => new { gl.TenantId, gl.LegalEntityId, gl.GoodsReceiptLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId }).HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => new { x.TenantId, x.BaseUomId }).HasPrincipalKey(u => new { u.TenantId, u.UomId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxCode>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PpnTaxCodeId }).HasPrincipalKey(tc => new { tc.TenantId, tc.LegalEntityId, tc.TaxCodeId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxCode>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.WithholdingTaxCodeId }).HasPrincipalKey(tc => new { tc.TenantId, tc.LegalEntityId, tc.TaxCodeId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceMatchEvaluation>(entity =>
        {
            entity.ToTable("invoice_match_evaluations", "ap", t =>
            {
                t.HasCheckConstraint("ck_inv_match_eval_stage", "evaluation_stage IN ('DRAFT_PREVIEW', 'SUBMIT', 'POST_COMMIT')");
                t.HasCheckConstraint("ck_inv_match_eval_outcomes", "dq_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND pp_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND bd_outcome IN ('MATCHED', 'WITHIN_TOLERANCE', 'VARIANCE_APPROVAL_REQUIRED', 'BLOCKED') AND overall_match_status IN ('MATCHED', 'WITHIN_TOLERANCE', 'APPROVAL_REQUIRED', 'POSTING_BLOCKED')");
            });

            entity.HasKey(x => x.EvaluationId);
            entity.Property(x => x.EvaluationId).HasColumnName("evaluation_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.PolicyId).HasColumnName("policy_id").IsRequired();
            entity.Property(x => x.EvaluationStage).HasColumnName("evaluation_stage").HasMaxLength(24).IsRequired();
            entity.Property(x => x.DqOutcome).HasColumnName("dq_outcome").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PpOutcome).HasColumnName("pp_outcome").HasMaxLength(32).IsRequired();
            entity.Property(x => x.BdOutcome).HasColumnName("bd_outcome").HasMaxLength(32).IsRequired();
            entity.Property(x => x.OverallMatchStatus).HasColumnName("overall_match_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.IsPostingBlocked).HasColumnName("is_posting_blocked").IsRequired();
            entity.Property(x => x.RequiresApproval).HasColumnName("requires_approval").IsRequired();
            entity.Property(x => x.RequiresPaymentBlock).HasColumnName("requires_payment_block").IsRequired();
            entity.Property(x => x.BlockingReasonsJson).HasColumnName("blocking_reasons_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ComparisonJson).HasColumnName("comparison_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.EvaluatedByUserId).HasColumnName("evaluated_by_user_id").IsRequired();
            entity.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluated_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId, x.EvaluatedAtUtc }).HasDatabaseName("ix_inv_match_eval_bill_time");
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<InvoiceMatchPolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyId }).HasPrincipalKey(p => new { p.TenantId, p.LegalEntityId, p.PolicyId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.EvaluatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierBillReceiptMatch>(entity =>
        {
            entity.ToTable("supplier_bill_receipt_matches", "ap", t =>
            {
                t.HasCheckConstraint("ck_bill_receipt_match_qty", "matched_base_quantity > 0 AND grni_cleared_amount > 0 AND covered_base_quantity >= 0");
            });

            entity.HasKey(x => x.MatchId);
            entity.Property(x => x.MatchId).HasColumnName("match_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id").IsRequired();
            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id").IsRequired();
            entity.Property(x => x.ReceiptAccrualLineId).HasColumnName("receipt_accrual_line_id").IsRequired();
            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id").IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.MatchedBaseQuantity).HasColumnName("matched_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ReceiptUnitCostBase).HasColumnName("receipt_unit_cost_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.InvoiceUnitPriceBase).HasColumnName("invoice_unit_price_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.GrniClearedAmount).HasColumnName("grni_cleared_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PriceVarianceAmount).HasColumnName("price_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.CoveredBaseQuantity).HasColumnName("covered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.InventoryAdjustmentAmount).HasColumnName("inventory_adjustment_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PnlVarianceAmount).HasColumnName("pnl_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).IsUnique().HasDatabaseName("ux_bill_receipt_match_bill_line");
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBillLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).HasPrincipalKey(l => new { l.TenantId, l.LegalEntityId, l.SupplierBillLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrual>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId }).HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.ReceiptAccrualId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrualLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualLineId }).HasPrincipalKey(al => new { al.TenantId, al.LegalEntityId, al.ReceiptAccrualLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayableItem>(entity =>
        {
            entity.ToTable("payable_items", "ap", t =>
            {
                t.HasCheckConstraint("ck_payable_items_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_payable_items_status", "status IN ('OPEN', 'PARTIALLY_PAID', 'PAID', 'CREDITED', 'REVERSED')");
                t.HasCheckConstraint("ck_payable_items_pay_block", "payment_block_status IN ('NONE', 'PAYMENT_BLOCKED', 'RELEASED')");
                t.HasCheckConstraint("ck_payable_items_amounts", "gross_bill_amount > 0 AND withholding_tax_amount >= 0 AND net_payable_amount > 0 AND paid_amount >= 0 AND credited_amount >= 0 AND remaining_open_amount >= 0 AND net_payable_amount = paid_amount + credited_amount + remaining_open_amount");
            });

            entity.HasKey(x => x.PayableItemId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PayableItemId });
            entity.Property(x => x.PayableItemId).HasColumnName("payable_item_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.PayableNumber).HasColumnName("payable_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierInvoiceNumber).HasColumnName("supplier_invoice_number").HasMaxLength(64).IsRequired();
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
            entity.Property(x => x.GrossBillAmount).HasColumnName("gross_bill_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.WithholdingTaxAmount).HasColumnName("withholding_tax_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.NetPayableAmount).HasColumnName("net_payable_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.CreditedAmount).HasColumnName("credited_amount").HasColumnType("numeric(18,4)").HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.RemainingOpenAmount).HasColumnName("remaining_open_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PaymentBlockStatus).HasColumnName("payment_block_status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.PaymentBlockReason).HasColumnName("payment_block_reason").HasMaxLength(300);
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PayableNumber }).IsUnique().HasDatabaseName("ux_payable_items_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).IsUnique().HasDatabaseName("ux_payable_items_bill");
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Partner>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId }).HasPrincipalKey(p => new { p.TenantId, p.PartnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptAccrualClearing>(entity =>
        {
            entity.ToTable("receipt_accrual_clearings", "gl", t =>
            {
                t.HasCheckConstraint("ck_receipt_accrual_clearings_pos", "cleared_base_quantity > 0 AND cleared_amount > 0");
            });

            entity.HasKey(x => x.ClearingId);
            entity.Property(x => x.ClearingId).HasColumnName("clearing_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id").IsRequired();
            entity.Property(x => x.ReceiptAccrualLineId).HasColumnName("receipt_accrual_line_id").IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id").IsRequired();
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id").IsRequired();
            entity.Property(x => x.ClearedBaseQuantity).HasColumnName("cleared_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.ClearedAmount).HasColumnName("cleared_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).IsUnique().HasDatabaseName("ux_receipt_accrual_clearings_bill_line");
            entity.HasOne<ReceiptAccrual>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId }).HasPrincipalKey(a => new { a.TenantId, a.LegalEntityId, a.ReceiptAccrualId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrualLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualLineId }).HasPrincipalKey(al => new { al.TenantId, al.LegalEntityId, al.ReceiptAccrualLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoicePriceAdjustment>(entity =>
        {
            entity.ToTable("invoice_price_adjustments", "inventory", t =>
            {
                t.HasCheckConstraint("ck_inv_price_adj_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_inv_price_adj_qty", "billed_base_quantity > 0 AND pool_on_hand_before_qty >= 0 AND covered_base_quantity >= 0 AND uncovered_base_quantity >= 0 AND billed_base_quantity = covered_base_quantity + uncovered_base_quantity AND total_price_diff_amount = inventory_adjustment_amount + pnl_variance_amount");
            });

            entity.HasKey(x => x.AdjustmentId);
            entity.Property(x => x.AdjustmentId).HasColumnName("adjustment_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id").IsRequired();
            entity.Property(x => x.BillNumber).HasColumnName("bill_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ValuationPoolId).HasColumnName("valuation_pool_id").IsRequired();
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id").IsRequired();
            entity.Property(x => x.ValuationEntryId).HasColumnName("valuation_entry_id");
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.BilledBaseQuantity).HasColumnName("billed_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.PoolOnHandBeforeQty).HasColumnName("pool_on_hand_before_qty").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.CoveredBaseQuantity).HasColumnName("covered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UncoveredBaseQuantity).HasColumnName("uncovered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalPriceDiffAmount).HasColumnName("total_price_diff_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.InventoryAdjustmentAmount).HasColumnName("inventory_adjustment_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PnlVarianceAmount).HasColumnName("pnl_variance_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PreviousMovingAverageCost).HasColumnName("previous_moving_average_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.NewMovingAverageCost).HasColumnName("new_moving_average_cost").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).IsUnique().HasDatabaseName("ux_invoice_price_adj_bill_line");
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(b => new { b.TenantId, b.LegalEntityId, b.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ValuationPool>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuationPoolId }).HasPrincipalKey(vp => new { vp.TenantId, vp.LegalEntityId, vp.ValuationPoolId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ValuationEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuationEntryId }).HasPrincipalKey(ve => new { ve.TenantId, ve.LegalEntityId, ve.ValuationEntryId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaxDetermination>(entity =>
        {
            entity.ToTable("tax_determinations", "tax", t =>
            {
                t.HasCheckConstraint("ck_tax_determinations_role", "tax_role IN ('PPN_INPUT', 'PPH_WITHHOLDING', 'PPN_INPUT_REVERSAL')");
                t.HasCheckConstraint("ck_tax_determinations_amounts", "gross_amount > 0 AND taxable_base_amount >= 0 AND nominal_rate_percent >= 0 AND effective_rate_percent >= 0 AND tax_amount >= 0");
            });

            entity.HasKey(x => x.DeterminationId);
            entity.Property(x => x.DeterminationId).HasColumnName("determination_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceDocumentType).HasColumnName("source_document_type").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SourceDocumentId).HasColumnName("source_document_id").IsRequired();
            entity.Property(x => x.SourceDocumentLineId).HasColumnName("source_document_line_id").IsRequired();
            entity.Property(x => x.TaxRole).HasColumnName("tax_role").HasMaxLength(24).IsRequired();
            entity.Property(x => x.TaxCodeId).HasColumnName("tax_code_id").IsRequired();
            entity.Property(x => x.TaxCodeSnapshot).HasColumnName("tax_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.TaxFamily).HasColumnName("tax_family").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(16).IsRequired();
            entity.Property(x => x.TreatmentClass).HasColumnName("treatment_class").HasMaxLength(32).IsRequired();
            entity.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TaxableBaseAmount).HasColumnName("taxable_base_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.NominalRatePercent).HasColumnName("nominal_rate_percent").HasColumnType("numeric(7,4)").IsRequired();
            entity.Property(x => x.EffectiveRatePercent).HasColumnName("effective_rate_percent").HasColumnType("numeric(7,4)").IsRequired();
            entity.Property(x => x.TaxAmount).HasColumnName("tax_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.ReviewStatus).HasColumnName("review_status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.EvidenceStatus).HasColumnName("evidence_status").HasMaxLength(32).IsRequired();
            entity.Property(x => x.DocumentaryEvidenceRef).HasColumnName("documentary_evidence_ref").HasMaxLength(120);
            entity.Property(x => x.CounterpartyNpwpSnapshot).HasColumnName("counterparty_npwp_snapshot").HasMaxLength(64);
            entity.Property(x => x.TransactionDate).HasColumnName("transaction_date").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourceDocumentId, x.SourceDocumentLineId, x.TaxRole }).IsUnique().HasDatabaseName("ux_tax_determinations_doc_line_role");
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxCode>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TaxCodeId }).HasPrincipalKey(tc => new { tc.TenantId, tc.LegalEntityId, tc.TaxCodeId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReturnSequence>(entity =>
        {
            entity.ToTable("return_sequences", "procurement");
            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReplacementSequence>(entity =>
        {
            entity.ToTable("replacement_sequences", "procurement");
            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CreditSequence>(entity =>
        {
            entity.ToTable("credit_sequences", "ap");
            entity.HasKey(x => new { x.TenantId, x.LegalEntityId, x.FiscalYear });
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.FiscalYear).HasColumnName("fiscal_year").IsRequired();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierReturn>(entity =>
        {
            entity.ToTable("supplier_returns", "procurement", t =>
            {
                t.HasCheckConstraint("ck_supplier_returns_case", "return_case_type IN ('UNVALUED_DOCK_REJECTION', 'VALUED_UNBILLED_RETURN', 'VALUED_BILLED_RETURN')");
                t.HasCheckConstraint("ck_supplier_returns_mode", "resolution_mode IN ('CLOSE_WITHOUT_REPLACEMENT', 'REPLACEMENT_ONLY', 'AP_CREDIT_NOTE')");
                t.HasCheckConstraint("ck_supplier_returns_disposition", "matrix_disposition IN ('SUPPORTED', 'BLOCKED_POLICY_OD05', 'BLOCKED_POLICY_OD07', 'BLOCKED_CLOSED_PERIOD')");
                t.HasCheckConstraint("ck_supplier_returns_status", "status IN ('DRAFT', 'PENDING_APPROVAL', 'APPROVED', 'EXECUTED', 'CANCELLED')");
                t.HasCheckConstraint("ck_supplier_returns_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_supplier_returns_totals", "total_returned_base_quantity > 0 AND total_inventory_credit_amount >= 0 AND total_grni_reversal_amount >= 0 AND total_credit_subtotal_amount >= 0 AND total_ppn_reversal_amount >= 0 AND total_credit_note_amount >= 0");
            });

            entity.HasKey(x => x.SupplierReturnId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId });

            entity.Property(x => x.SupplierReturnId).HasColumnName("supplier_return_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.DraftCode).HasColumnName("draft_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReturnNumber).HasColumnName("return_number").HasMaxLength(40);
            entity.Property(x => x.ReturnCaseType).HasColumnName("return_case_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.ResolutionMode).HasColumnName("resolution_mode").HasMaxLength(32).IsRequired();
            entity.Property(x => x.MatrixDisposition).HasColumnName("matrix_disposition").HasMaxLength(32).IsRequired();
            entity.Property(x => x.MatrixBlockingCode).HasColumnName("matrix_blocking_code").HasMaxLength(80);
            entity.Property(x => x.MatrixBlockingReason).HasColumnName("matrix_blocking_reason").HasMaxLength(400);
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoNumberSnapshot).HasColumnName("po_number_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id");
            entity.Property(x => x.ReceiptNumberSnapshot).HasColumnName("receipt_number_snapshot").HasMaxLength(40);
            entity.Property(x => x.ReceiptAccrualId).HasColumnName("receipt_accrual_id");
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id");
            entity.Property(x => x.BillNumberSnapshot).HasColumnName("bill_number_snapshot").HasMaxLength(40);
            entity.Property(x => x.PayableItemId).HasColumnName("payable_item_id");
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierNpwpSnapshot).HasColumnName("supplier_npwp_snapshot").HasMaxLength(64);
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.ReturnDate).HasColumnName("return_date").IsRequired();
            entity.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(40).IsRequired();
            entity.Property(x => x.CarrierOrDispatchRef).HasColumnName("carrier_or_dispatch_ref").HasMaxLength(120);
            entity.Property(x => x.TaxReturnNoteNumber).HasColumnName("tax_return_note_number").HasMaxLength(64);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.ApprovalPolicyCode).HasColumnName("approval_policy_code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.LastApprovalDecision).HasColumnName("last_approval_decision").HasMaxLength(24);
            entity.Property(x => x.LastRejectionReason).HasColumnName("last_rejection_reason").HasMaxLength(300);
            entity.Property(x => x.SubmittedByUserId).HasColumnName("submitted_by_user_id");
            entity.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc");
            entity.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id");
            entity.Property(x => x.ApprovedAtUtc).HasColumnName("approved_at_utc");
            entity.Property(x => x.ExecutedByUserId).HasColumnName("executed_by_user_id");
            entity.Property(x => x.ExecutedAtUtc).HasColumnName("executed_at_utc");
            entity.Property(x => x.ExecuteIdempotencyKey).HasColumnName("execute_idempotency_key").HasMaxLength(80);
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id");
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id");
            entity.Property(x => x.SupplierCreditId).HasColumnName("supplier_credit_id");
            entity.Property(x => x.TotalReturnedBaseQuantity).HasColumnName("total_returned_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.TotalInventoryCreditAmount).HasColumnName("total_inventory_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalGrniReversalAmount).HasColumnName("total_grni_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCreditSubtotalAmount).HasColumnName("total_credit_subtotal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalPpnReversalAmount).HasColumnName("total_ppn_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCreditNoteAmount).HasColumnName("total_credit_note_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalPpvDifferenceAmount).HasColumnName("total_ppv_difference_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DraftCode }).IsUnique().HasDatabaseName("ux_supplier_returns_draft_code");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReturnNumber }).IsUnique().HasFilter("return_number IS NOT NULL").HasDatabaseName("ux_supplier_returns_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ExecuteIdempotencyKey }).IsUnique().HasFilter("execute_idempotency_key IS NOT NULL").HasDatabaseName("ux_supplier_returns_execute_idempotency");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Status, x.ReturnDate }).HasDatabaseName("ix_supplier_returns_status_date");

            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId }).HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GoodsReceipt>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId }).HasPrincipalKey(gr => new { gr.TenantId, gr.LegalEntityId, gr.GoodsReceiptId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrual>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualId }).HasPrincipalKey(ra => new { ra.TenantId, ra.LegalEntityId, ra.ReceiptAccrualId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(sb => new { sb.TenantId, sb.LegalEntityId, sb.SupplierBillId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PayableItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PayableItemId }).HasPrincipalKey(pi => new { pi.TenantId, pi.LegalEntityId, pi.PayableItemId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Partner>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId }).HasPrincipalKey(p => new { p.TenantId, p.PartnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId }).HasPrincipalKey(sm => new { sm.TenantId, sm.LegalEntityId, sm.StockMovementId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierReturnLine>(entity =>
        {
            entity.ToTable("supplier_return_lines", "procurement", t =>
            {
                t.HasCheckConstraint("ck_supplier_return_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_supplier_return_lines_amounts", "returned_base_quantity > 0 AND authorized_unit_cost_base > 0 AND inventory_credit_amount >= 0 AND grni_reversal_amount >= 0 AND credit_subtotal_amount >= 0 AND ppn_reversal_amount >= 0 AND total_credit_amount >= 0");
            });

            entity.HasKey(x => x.SupplierReturnLineId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnLineId });

            entity.Property(x => x.SupplierReturnLineId).HasColumnName("supplier_return_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierReturnId).HasColumnName("supplier_return_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.GoodsReceiptLineId).HasColumnName("goods_receipt_line_id").IsRequired();
            entity.Property(x => x.ValuedAcceptanceLineId).HasColumnName("valued_acceptance_line_id");
            entity.Property(x => x.ReceiptAccrualLineId).HasColumnName("receipt_accrual_line_id");
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id");
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.LineageLineKey).HasColumnName("lineage_line_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.ItemCodeSnapshot).HasColumnName("item_code_snapshot").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ItemNameSnapshot).HasColumnName("item_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.SourceLocationId).HasColumnName("source_location_id");
            entity.Property(x => x.SourceStockLotId).HasColumnName("source_stock_lot_id");
            entity.Property(x => x.SourceStockStatus).HasColumnName("source_stock_status").HasMaxLength(24);
            entity.Property(x => x.ReturnedBaseQuantity).HasColumnName("returned_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AuthorizedUnitCostBase).HasColumnName("authorized_unit_cost_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.InventoryCreditAmount).HasColumnName("inventory_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.GrniReversalAmount).HasColumnName("grni_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.CreditSubtotalAmount).HasColumnName("credit_subtotal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnReversalAmount).HasColumnName("ppn_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCreditAmount).HasColumnName("total_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpvDifferenceAmount).HasColumnName("ppv_difference_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.LineNotes).HasColumnName("line_notes").HasMaxLength(240);

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId, x.LineNumber }).IsUnique().HasDatabaseName("ux_supplier_return_lines_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId }).HasDatabaseName("ix_supplier_return_lines_gr_line");

            entity.HasOne<SupplierReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId }).HasPrincipalKey(sr => new { sr.TenantId, sr.LegalEntityId, sr.SupplierReturnId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId }).HasPrincipalKey(gl => new { gl.TenantId, gl.LegalEntityId, gl.GoodsReceiptLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ValuedAcceptanceLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ValuedAcceptanceLineId }).HasPrincipalKey(vl => new { vl.TenantId, vl.LegalEntityId, vl.ValuedAcceptanceLineId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReceiptAccrualLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceiptAccrualLineId }).HasPrincipalKey(al => new { al.TenantId, al.LegalEntityId, al.ReceiptAccrualLineId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBillLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).HasPrincipalKey(bl => new { bl.TenantId, bl.LegalEntityId, bl.SupplierBillLineId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId }).HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => new { x.TenantId, x.BaseUomId }).HasPrincipalKey(u => new { u.TenantId, u.UomId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WarehouseLocation>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceLocationId }).HasPrincipalKey(wl => new { wl.TenantId, wl.LegalEntityId, wl.LocationId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StockLot>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourceStockLotId }).HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.StockLotId }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReplacementAuthorization>(entity =>
        {
            entity.ToTable("replacement_authorizations", "procurement", t =>
            {
                t.HasCheckConstraint("ck_replacement_auth_case", "source_case_type IN ('UNVALUED_DOCK_REJECTION', 'VALUED_UNBILLED_RETURN', 'VALUED_BILLED_RETURN')");
                t.HasCheckConstraint("ck_replacement_auth_status", "status IN ('ACTIVE', 'PARTIALLY_RECEIVED', 'FULFILLED', 'CANCELLED')");
                t.HasCheckConstraint("ck_replacement_auth_qty", "authorized_base_quantity > 0 AND delivered_base_quantity >= 0 AND accepted_base_quantity >= 0 AND remaining_base_quantity >= 0 AND accepted_base_quantity <= delivered_base_quantity AND authorized_base_quantity = delivered_base_quantity + remaining_base_quantity");
                t.HasCheckConstraint("ck_replacement_auth_cost", "commercial_incremental_unit_price = 0 AND authorized_inventory_unit_cost_base > 0");
            });

            entity.HasKey(x => x.ReplacementAuthorizationId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ReplacementAuthorizationId });

            entity.Property(x => x.ReplacementAuthorizationId).HasColumnName("replacement_authorization_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.AuthorizationNumber).HasColumnName("authorization_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierReturnId).HasColumnName("supplier_return_id").IsRequired();
            entity.Property(x => x.SupplierReturnLineId).HasColumnName("supplier_return_line_id").IsRequired();
            entity.Property(x => x.SourceCaseType).HasColumnName("source_case_type").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id").IsRequired();
            entity.Property(x => x.PoLineId).HasColumnName("po_line_id").IsRequired();
            entity.Property(x => x.LineageLineKey).HasColumnName("lineage_line_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceGoodsReceiptId).HasColumnName("source_goods_receipt_id");
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.BaseUomId).HasColumnName("base_uom_id").IsRequired();
            entity.Property(x => x.BaseUomCodeSnapshot).HasColumnName("base_uom_code_snapshot").HasMaxLength(32).IsRequired();
            entity.Property(x => x.AuthorizedBaseQuantity).HasColumnName("authorized_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.DeliveredBaseQuantity).HasColumnName("delivered_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AcceptedBaseQuantity).HasColumnName("accepted_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.RemainingBaseQuantity).HasColumnName("remaining_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.CommercialIncrementalUnitPrice).HasColumnName("commercial_incremental_unit_price").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.AuthorizedInventoryUnitCostBase).HasColumnName("authorized_inventory_unit_cost_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
            entity.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
            entity.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1L).IsConcurrencyToken().IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AuthorizationNumber }).IsUnique().HasDatabaseName("ux_replacement_auth_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnLineId }).IsUnique().HasDatabaseName("ux_replacement_auth_return_line");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.Status }).HasDatabaseName("ix_replacement_auth_po_status");

            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId }).HasPrincipalKey(sr => new { sr.TenantId, sr.LegalEntityId, sr.SupplierReturnId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierReturnLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnLineId }).HasPrincipalKey(sl => new { sl.TenantId, sl.LegalEntityId, sl.SupplierReturnLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId }).HasPrincipalKey(po => new { po.TenantId, po.LegalEntityId, po.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PoLineId }).HasPrincipalKey(pl => new { pl.TenantId, pl.LegalEntityId, pl.PoLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Partner>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId }).HasPrincipalKey(p => new { p.TenantId, p.PartnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => new { x.TenantId, x.BaseUomId }).HasPrincipalKey(u => new { u.TenantId, u.UomId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.ApprovedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierCredit>(entity =>
        {
            entity.ToTable("supplier_credits", "ap", t =>
            {
                t.HasCheckConstraint("ck_supplier_credits_currency", "currency_code = 'IDR'");
                t.HasCheckConstraint("ck_supplier_credits_amounts", "subtotal_credit_amount > 0 AND ppn_reversal_amount >= 0 AND total_credit_amount = subtotal_credit_amount + ppn_reversal_amount AND inventory_credit_amount > 0 AND (subtotal_credit_amount - inventory_credit_amount) = ppv_difference_amount");
            });

            entity.HasKey(x => x.SupplierCreditId);
            entity.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierCreditId });

            entity.Property(x => x.SupplierCreditId).HasColumnName("supplier_credit_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.CreditNumber).HasColumnName("credit_number").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierReturnId).HasColumnName("supplier_return_id").IsRequired();
            entity.Property(x => x.ReturnNumberSnapshot).HasColumnName("return_number_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SupplierBillId).HasColumnName("supplier_bill_id").IsRequired();
            entity.Property(x => x.BillNumberSnapshot).HasColumnName("bill_number_snapshot").HasMaxLength(40).IsRequired();
            entity.Property(x => x.PayableItemId).HasColumnName("payable_item_id").IsRequired();
            entity.Property(x => x.SupplierPartnerId).HasColumnName("supplier_partner_id").IsRequired();
            entity.Property(x => x.SupplierCodeSnapshot).HasColumnName("supplier_code_snapshot").HasMaxLength(48).IsRequired();
            entity.Property(x => x.SupplierLegalNameSnapshot).HasColumnName("supplier_legal_name_snapshot").HasMaxLength(200).IsRequired();
            entity.Property(x => x.SupplierNpwpSnapshot).HasColumnName("supplier_npwp_snapshot").HasMaxLength(64);
            entity.Property(x => x.TaxReturnNoteNumber).HasColumnName("tax_return_note_number").HasMaxLength(64);
            entity.Property(x => x.PostingDate).HasColumnName("posting_date").IsRequired();
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
            entity.Property(x => x.SubtotalCreditAmount).HasColumnName("subtotal_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnReversalAmount).HasColumnName("ppn_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCreditAmount).HasColumnName("total_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.InventoryCreditAmount).HasColumnName("inventory_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpvDifferenceAmount).HasColumnName("ppv_difference_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.GlJournalId).HasColumnName("gl_journal_id").IsRequired();
            entity.Property(x => x.StockMovementId).HasColumnName("stock_movement_id").IsRequired();
            entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CreditNumber }).IsUnique().HasDatabaseName("ux_supplier_credits_number");
            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId }).IsUnique().HasDatabaseName("ux_supplier_credits_return");

            entity.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).HasPrincipalKey(e => new { e.TenantId, e.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierReturn>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnId }).HasPrincipalKey(sr => new { sr.TenantId, sr.LegalEntityId, sr.SupplierReturnId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBill>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillId }).HasPrincipalKey(sb => new { sb.TenantId, sb.LegalEntityId, sb.SupplierBillId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PayableItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PayableItemId }).HasPrincipalKey(pi => new { pi.TenantId, pi.LegalEntityId, pi.PayableItemId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Partner>().WithMany().HasForeignKey(x => new { x.TenantId, x.SupplierPartnerId }).HasPrincipalKey(p => new { p.TenantId, p.PartnerId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GlJournalId }).HasPrincipalKey(j => new { j.TenantId, j.LegalEntityId, j.JournalId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.StockMovementId }).HasPrincipalKey(sm => new { sm.TenantId, sm.LegalEntityId, sm.StockMovementId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TenantMembership>().WithMany().HasForeignKey(x => new { x.TenantId, x.CreatedByUserId }).HasPrincipalKey(m => new { m.TenantId, m.UserId }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierCreditLine>(entity =>
        {
            entity.ToTable("supplier_credit_lines", "ap", t =>
            {
                t.HasCheckConstraint("ck_supplier_credit_lines_number", "line_number >= 1");
                t.HasCheckConstraint("ck_supplier_credit_lines_amounts", "credited_base_quantity > 0 AND unit_price_base > 0 AND subtotal_credit_amount > 0 AND ppn_reversal_amount >= 0 AND total_credit_amount = subtotal_credit_amount + ppn_reversal_amount AND inventory_credit_amount > 0 AND (subtotal_credit_amount - inventory_credit_amount) = ppv_difference_amount");
            });

            entity.HasKey(x => x.SupplierCreditLineId);
            entity.Property(x => x.SupplierCreditLineId).HasColumnName("supplier_credit_line_id").ValueGeneratedNever();
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.LegalEntityId).HasColumnName("legal_entity_id").IsRequired();
            entity.Property(x => x.SupplierCreditId).HasColumnName("supplier_credit_id").IsRequired();
            entity.Property(x => x.LineNumber).HasColumnName("line_number").IsRequired();
            entity.Property(x => x.SupplierReturnLineId).HasColumnName("supplier_return_line_id").IsRequired();
            entity.Property(x => x.SupplierBillLineId).HasColumnName("supplier_bill_line_id").IsRequired();
            entity.Property(x => x.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(x => x.CreditedBaseQuantity).HasColumnName("credited_base_quantity").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.UnitPriceBase).HasColumnName("unit_price_base").HasColumnType("numeric(18,6)").IsRequired();
            entity.Property(x => x.SubtotalCreditAmount).HasColumnName("subtotal_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpnReversalAmount).HasColumnName("ppn_reversal_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.TotalCreditAmount).HasColumnName("total_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.InventoryCreditAmount).HasColumnName("inventory_credit_amount").HasColumnType("numeric(18,4)").IsRequired();
            entity.Property(x => x.PpvDifferenceAmount).HasColumnName("ppv_difference_amount").HasColumnType("numeric(18,4)").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierCreditId, x.LineNumber }).IsUnique().HasDatabaseName("ux_supplier_credit_lines_number");

            entity.HasOne<SupplierCredit>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierCreditId }).HasPrincipalKey(sc => new { sc.TenantId, sc.LegalEntityId, sc.SupplierCreditId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierReturnLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierReturnLineId }).HasPrincipalKey(rl => new { rl.TenantId, rl.LegalEntityId, rl.SupplierReturnLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SupplierBillLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBillLineId }).HasPrincipalKey(bl => new { bl.TenantId, bl.LegalEntityId, bl.SupplierBillLineId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Item>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).HasPrincipalKey(i => new { i.TenantId, i.ItemId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
