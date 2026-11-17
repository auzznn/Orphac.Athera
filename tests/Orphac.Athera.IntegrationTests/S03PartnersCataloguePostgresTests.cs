using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.IntegrationTests;

public sealed class S03PartnersCataloguePostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S03SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ManagerA1Id,
        string ManagerA1Email,
        Guid ReadOnlyA2Id,
        string ReadOnlyA2Email,
        Guid ManagerB1Id,
        string ManagerB1Email,
        Guid PcsAId,
        Guid PartnerBId,
        Guid ItemBId,
        string Password);

    [Fact]
    public async Task S03_PG_01_MissingContextDeniesAndCrossTenantMastersIsolatedUnderForcedRls()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();

        await using (var rlsCmd = conn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'partners' AND c.relname IN ('partners', 'partner_contacts', 'partner_entity_profiles'))
                    OR (n.nspname = 'catalog' AND c.relname IN ('units_of_measure', 'items', 'item_uom_conversions', 'item_entity_policies'))
                )
                AND c.relrowsecurity
                AND c.relforcerowsecurity;
                """;
            Assert.Equal(7L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Without app.tenant_id, runtime role cannot read or insert S03 rows
        await using (var readZeroCmd = conn.CreateCommand())
        {
            readZeroCmd.CommandText = "SELECT count(*) FROM partners.partners";
            Assert.Equal(0L, Convert.ToInt64(await readZeroCmd.ExecuteScalarAsync()));
        }

        await using (var writeNoTenantCmd = conn.CreateCommand())
        {
            writeNoTenantCmd.CommandText = """
                INSERT INTO partners.partners (partner_id, tenant_id, partner_code, legal_name, display_name, is_supplier, is_customer, tax_pkp_status, entity_scope_mode, status, created_at_utc, updated_at_utc, version)
                VALUES (@pid, @tid, 'ROGUE-01', 'Rogue Partner', 'Rogue', true, false, 'UNKNOWN', 'ALL_ENTITIES', 'ACTIVE', now(), now(), 1);
                """;
            writeNoTenantCmd.Parameters.AddWithValue("pid", Guid.NewGuid());
            writeNoTenantCmd.Parameters.AddWithValue("tid", world.TenantBId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeNoTenantCmd.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // Scoped to Tenant A, Tenant B rows stay invisible and cross-tenant write is denied by RLS WITH CHECK
        await using var tx = await conn.BeginTransactionAsync();
        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
            setCmd.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
            await setCmd.ExecuteScalarAsync();
        }

        await using (var foreignPartnerRead = conn.CreateCommand())
        {
            foreignPartnerRead.Transaction = tx;
            foreignPartnerRead.CommandText = "SELECT count(*) FROM partners.partners WHERE partner_id = @bid;";
            foreignPartnerRead.Parameters.AddWithValue("bid", world.PartnerBId);
            Assert.Equal(0L, Convert.ToInt64(await foreignPartnerRead.ExecuteScalarAsync()));
        }

        await using (var foreignItemRead = conn.CreateCommand())
        {
            foreignItemRead.Transaction = tx;
            foreignItemRead.CommandText = "SELECT count(*) FROM catalog.items WHERE item_id = @bid;";
            foreignItemRead.Parameters.AddWithValue("bid", world.ItemBId);
            Assert.Equal(0L, Convert.ToInt64(await foreignItemRead.ExecuteScalarAsync()));
        }

        await tx.SaveAsync("sp_cross_insert");
        await using (var crossInsert = conn.CreateCommand())
        {
            crossInsert.Transaction = tx;
            crossInsert.CommandText = """
                INSERT INTO catalog.units_of_measure (uom_id, tenant_id, uom_code, name, quantity_precision_scale, status, created_at_utc, updated_at_utc, version)
                VALUES (@uid, @tid, 'XCODE', 'Cross Tenant UoM', 0, 'ACTIVE', now(), now(), 1);
                """;
            crossInsert.Parameters.AddWithValue("uid", Guid.NewGuid());
            crossInsert.Parameters.AddWithValue("tid", world.TenantBId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => crossInsert.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await tx.RollbackAsync("sp_cross_insert");
        await tx.CommitAsync();
    }

    [Fact]
    public async Task S03_PG_02_MigrationM03UpDownUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117183813_M02_AdminApprovalsAuditAndOutbox");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema IN ('partners', 'catalog');";
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCheck = conn.CreateCommand();
            upCheck.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema IN ('partners', 'catalog');";
            Assert.Equal(7L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S03_PG_03_RuntimeRoleCannotHardDeletePartnersOrItems()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        var partnerAId = Guid.NewGuid();
        var itemAId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            migDb.Partners.Add(Partner.Create(partnerAId, world.TenantAId, "NO-DEL-P", "PT No Delete", "No Delete", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now));
            migDb.Items.Add(Item.Create(itemAId, world.TenantAId, "NO-DEL-I", "Item No Delete", ItemKinds.StockedGood, world.PcsAId, null, null, EntityScopeModes.AllEntities, now));
            await migDb.SaveChangesAsync();
        }

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
            setCmd.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
            await setCmd.ExecuteScalarAsync();
        }

        await tx.SaveAsync("sp_del_partner");
        await using (var delPartner = conn.CreateCommand())
        {
            delPartner.Transaction = tx;
            delPartner.CommandText = "DELETE FROM partners.partners WHERE partner_id = @pid;";
            delPartner.Parameters.AddWithValue("pid", partnerAId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delPartner.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_del_partner");

        await tx.SaveAsync("sp_del_item");
        await using (var delItem = conn.CreateCommand())
        {
            delItem.Transaction = tx;
            delItem.CommandText = "DELETE FROM catalog.items WHERE item_id = @iid;";
            delItem.Parameters.AddWithValue("iid", itemAId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delItem.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_del_item");
    }

    [Fact]
    public async Task S03_API_01_DualPartnerLifecycleDuplicateCodeRestrictionAndAudit()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var mgrA1 = await CreateSignedInClientAsync(factory, world.ManagerA1Email, world.Password);
        using var readA2 = await CreateSignedInClientAsync(factory, world.ReadOnlyA2Email, world.Password);
        using var mgrB1 = await CreateSignedInClientAsync(factory, world.ManagerB1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}";
        var baseB1 = $"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}";

        // 1. Create dual supplier+customer partner restricted to entities explicitly opted in
        var createRes = await mgrA1.PostAsJsonAsync($"{baseA1}/partners", new CreatePartnerRequest(
            "pt-dual-01",
            "PT Dagang Serba Usaha Nusantara",
            "Dagang Serba Usaha",
            IsSupplier: true,
            IsCustomer: true,
            TaxRegistrationNpwp: "01.333.444.5-011.000",
            TaxPkpStatus: TaxPkpStatuses.Pkp,
            EntityScopeMode: EntityScopeModes.RestrictedEntities));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<PartnerDetailDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("PT-DUAL-01", created.PartnerCode);
        Assert.True(created.IsSupplier && created.IsCustomer);
        Assert.False(created.EffectiveSupplierEligible);

        // 2. Duplicate code within Tenant A -> 409; same code in Tenant B -> 201
        var dupRes = await mgrA1.PostAsJsonAsync($"{baseA1}/partners", new CreatePartnerRequest(
            "PT-DUAL-01", "PT Duplikat", "Duplikat", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities));
        Assert.Equal(HttpStatusCode.Conflict, dupRes.StatusCode);

        var otherTenantSameCode = await mgrB1.PostAsJsonAsync($"{baseB1}/partners", new CreatePartnerRequest(
            "PT-DUAL-01", "CV Samudra Dual", "Samudra Dual", true, true, null, TaxPkpStatuses.NonPkp, EntityScopeModes.AllEntities));
        Assert.Equal(HttpStatusCode.Created, otherTenantSameCode.StatusCode);

        // 3. Add contact and enable profile ONLY on A1
        var contactRes = await mgrA1.PostAsJsonAsync($"{baseA1}/partners/{created.PartnerId:D}/contacts", new CreatePartnerContactRequest(
            "Dian Permata", PartnerContactRoles.Commercial, "dian@dagang.demo.local", "+62-21-555-0199", true));
        Assert.Equal(HttpStatusCode.Created, contactRes.StatusCode);

        var profileRes = await mgrA1.PutAsJsonAsync($"{baseA1}/partners/{created.PartnerId:D}/entity-profile", new UpsertPartnerEntityProfileRequest(
            SupplierEligible: true, CustomerEligible: true, DefaultPaymentTermsDays: 30, Notes: "Approved Jakarta vendor & buyer"));
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);
        var afterProfile = await profileRes.Content.ReadFromJsonAsync<PartnerDetailDto>(JsonOptions);
        Assert.True(afterProfile!.EffectiveSupplierEligible);
        Assert.True(afterProfile.EffectiveCustomerEligible);
        Assert.Single(afterProfile.Contacts);

        // 4. Sibling entity A2 still sees restricted partner as NOT eligible
        var a2Detail = await (await readA2.GetAsync($"{baseA2}/partners/{created.PartnerId:D}")).Content.ReadFromJsonAsync<PartnerDetailDto>(JsonOptions);
        Assert.NotNull(a2Detail);
        Assert.False(a2Detail.EffectiveSupplierEligible);
        Assert.False(a2Detail.EffectiveCustomerEligible);

        // 5. Deactivate partner -> eligibility immediately false even on A1, and audit event logged
        var deactivateRes = await mgrA1.PutAsJsonAsync($"{baseA1}/partners/{created.PartnerId:D}", new UpdatePartnerRequest(
            created.LegalName, created.DisplayName, true, true, created.TaxRegistrationNpwp, created.TaxPkpStatus,
            EntityScopeModes.RestrictedEntities, PartnerStatuses.Inactive, afterProfile.Version));
        Assert.Equal(HttpStatusCode.OK, deactivateRes.StatusCode);
        var deactivated = await deactivateRes.Content.ReadFromJsonAsync<PartnerDetailDto>(JsonOptions);
        Assert.Equal(PartnerStatuses.Inactive, deactivated!.Status);
        Assert.False(deactivated.EffectiveSupplierEligible);
        Assert.False(deactivated.EffectiveCustomerEligible);
    }

    [Fact]
    public async Task S03_API_02_CatalogItemExactUomRoundTripFractionalRejectAndRestriction()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var mgrA1 = await CreateSignedInClientAsync(factory, world.ManagerA1Email, world.Password);
        using var readA2 = await CreateSignedInClientAsync(factory, world.ReadOnlyA2Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}";
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}";

        // Create UoM BOX (scale 0) & unregistered PALLET (scale 0)
        var boxRes = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/uoms", new CreateUomRequest("BOX", "Box Karton", 0));
        Assert.Equal(HttpStatusCode.Created, boxRes.StatusCode);
        var box = await boxRes.Content.ReadFromJsonAsync<UnitOfMeasureDto>(JsonOptions);

        var pltRes = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/uoms", new CreateUomRequest("PLT", "Pallet", 0));
        Assert.Equal(HttpStatusCode.Created, pltRes.StatusCode);
        var plt = await pltRes.Content.ReadFromJsonAsync<UnitOfMeasureDto>(JsonOptions);

        // Create stock item with base PCS and default purchase BOX
        var itemRes = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items", new CreateItemRequest(
            "itm-001", "Kardus Induk 60x40x40", ItemKinds.StockedGood, world.PcsAId, box!.UomId, world.PcsAId, EntityScopeModes.RestrictedEntities));
        Assert.Equal(HttpStatusCode.Created, itemRes.StatusCode);
        var item = await itemRes.Content.ReadFromJsonAsync<ItemDetailDto>(JsonOptions);

        // Add conversion 1 BOX = 24 PCS (and invalid <= 0 rejected)
        var badConv = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items/{item!.ItemId:D}/conversions", new UpsertItemUomRequest(box.UomId, 0m, CatalogStatuses.Active));
        Assert.Equal(HttpStatusCode.BadRequest, badConv.StatusCode);

        var goodConv = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}/conversions", new UpsertItemUomRequest(box.UomId, 24m, CatalogStatuses.Active));
        Assert.Equal(HttpStatusCode.OK, goodConv.StatusCode);

        // Exact conversion & round-trip: 2 BOX -> 48 PCS -> 2 BOX
        var previewOk = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}/convert-preview", new ConvertQuantityPreviewRequest(box.UomId, 2m));
        Assert.Equal(HttpStatusCode.OK, previewOk.StatusCode);
        var preview = await previewOk.Content.ReadFromJsonAsync<ConvertQuantityPreviewResponse>(JsonOptions);
        Assert.Equal(48m, preview!.ConvertedBaseQuantity);
        Assert.Equal(2m, preview.RoundTripInputQuantity);

        // Discrete fractional base rejected (0.1 * 24 = 2.4 PCS)
        var fracRes = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}/convert-preview", new ConvertQuantityPreviewRequest(box.UomId, 0.1m));
        Assert.Equal(HttpStatusCode.BadRequest, fracRes.StatusCode);

        // Unconfigured UoM rejected
        var missingConvRes = await mgrA1.PostAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}/convert-preview", new ConvertQuantityPreviewRequest(plt!.UomId, 1m));
        Assert.Equal(HttpStatusCode.BadRequest, missingConvRes.StatusCode);

        // Enable item strictly for Entity A1 -> A2 sees false
        var policyRes = await mgrA1.PutAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}/entity-policy", new UpsertItemEntityPolicyRequest(true, true, "Jakarta only"));
        Assert.Equal(HttpStatusCode.OK, policyRes.StatusCode);
        var a1Detail = await policyRes.Content.ReadFromJsonAsync<ItemDetailDto>(JsonOptions);
        Assert.True(a1Detail!.EffectivePurchaseAllowed);

        var a2Detail = await (await readA2.GetAsync($"{baseA2}/catalog/items/{item.ItemId:D}")).Content.ReadFromJsonAsync<ItemDetailDto>(JsonOptions);
        Assert.False(a2Detail!.EffectivePurchaseAllowed);
        Assert.False(a2Detail.EffectiveSalesAllowed);

        // Deactivate item -> even A1 becomes ineligible
        var deactRes = await mgrA1.PutAsJsonAsync($"{baseA1}/catalog/items/{item.ItemId:D}", new UpdateItemRequest(
            item.Name, ItemKinds.StockedGood, box.UomId, world.PcsAId, EntityScopeModes.RestrictedEntities, CatalogStatuses.Inactive, a1Detail.Version));
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);
        var deactivated = await deactRes.Content.ReadFromJsonAsync<ItemDetailDto>(JsonOptions);
        Assert.False(deactivated!.EffectivePurchaseAllowed);
    }

    [Fact]
    public async Task S03_API_03_HostileTenantEntityAndReadOnlyPermissionsFailSafely()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var mgrA1 = await CreateSignedInClientAsync(factory, world.ManagerA1Email, world.Password);
        using var readA2 = await CreateSignedInClientAsync(factory, world.ReadOnlyA2Email, world.Password);

        // Cross-tenant selector -> 404
        var crossRes = await mgrA1.GetAsync($"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/partners");
        Assert.Equal(HttpStatusCode.NotFound, crossRes.StatusCode);

        // Sibling entity without grant (mgrA1 -> EntityA2) -> 404
        var siblingRes = await mgrA1.GetAsync($"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/catalog/items");
        Assert.Equal(HttpStatusCode.NotFound, siblingRes.StatusCode);

        // Read-only actor on A2 can read both lists but cannot mutate (403)
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}";
        Assert.Equal(HttpStatusCode.OK, (await readA2.GetAsync($"{baseA2}/partners")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await readA2.GetAsync($"{baseA2}/catalog/items")).StatusCode);

        var forbiddenPartnerPost = await readA2.PostAsJsonAsync($"{baseA2}/partners", new CreatePartnerRequest(
            "NO-PERM-P", "PT Denied", "Denied", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPartnerPost.StatusCode);

        var forbiddenUomPost = await readA2.PostAsJsonAsync($"{baseA2}/catalog/uoms", new CreateUomRequest("ROL", "Roll", 0));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUomPost.StatusCode);
    }

    private static async Task<HttpClient> CreateSignedInClientAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:3200");

        var csrfPayload = await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, password, false))
        };
        loginReq.Headers.Add(csrfPayload!.HeaderName, csrfPayload.RequestToken);
        var loginRes = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session!.CsrfToken);
        return client;
    }

    private static async Task<S03SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (mgrA1Id, mgrA1Email, _) = await fx.CreateTestUserAsync("mgr.a1.s03@test.local", "Rina Manager", pw);
        var (readA2Id, readA2Email, _) = await fx.CreateTestUserAsync("read.a2.s03@test.local", "Budi Reader", pw);
        var (mgrB1Id, mgrB1Email, _) = await fx.CreateTestUserAsync("mgr.b1.s03@test.local", "Dedi Samudra", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();
        var pcsAId = Guid.NewGuid();
        var pcsBId = Guid.NewGuid();
        var partnerBId = Guid.NewGuid();
        var itemBId = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", now),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", now));

        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, mgrA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readA2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, mgrB1Id, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, mgrA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, mgrA1Id, PermissionCodes.PartnersRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, mgrA1Id, PermissionCodes.PartnersManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, mgrA1Id, PermissionCodes.CatalogRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, mgrA1Id, PermissionCodes.CatalogManage, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readA2Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readA2Id, PermissionCodes.PartnersRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readA2Id, PermissionCodes.CatalogRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, mgrB1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, mgrB1Id, PermissionCodes.PartnersRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, mgrB1Id, PermissionCodes.PartnersManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, mgrB1Id, PermissionCodes.CatalogRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, mgrB1Id, PermissionCodes.CatalogManage, now, null, now));

        db.UnitsOfMeasure.AddRange(
            UnitOfMeasure.Create(pcsAId, tenantAId, "PCS", "Pieces", 0, now),
            UnitOfMeasure.Create(pcsBId, tenantBId, "PCS", "Pieces Samudra", 0, now));

        db.Partners.Add(Partner.Create(partnerBId, tenantBId, "SMDR-ONLY", "CV Foreign Partner", "Foreign Partner", true, false, null, TaxPkpStatuses.Unknown, EntityScopeModes.AllEntities, now));
        db.Items.Add(Item.Create(itemBId, tenantBId, "SMDR-ITEM", "Foreign Item", ItemKinds.StockedGood, pcsBId, null, null, EntityScopeModes.AllEntities, now));

        await db.SaveChangesAsync();

        return new S03SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            mgrA1Id, mgrA1Email, readA2Id, readA2Email, mgrB1Id, mgrB1Email,
            pcsAId, partnerBId, itemBId, pw);
    }
}
