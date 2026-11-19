using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Platform;
using Xunit;

namespace Orphac.Athera.IntegrationTests;

public sealed class S18ReleaseCandidatePostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task S18_SEC_01_CompleteRlsAuditLeastPrivilegeRoleAndHostileCrossTenantApiMatrix()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedRcWorldAsync(fx);

        // 1. Verify athera_app least-privilege database principal
        await using (var migConn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await migConn.OpenAsync();
            await using var roleCmd = new NpgsqlCommand("""
                SELECT rolsuper, rolbypassrls
                FROM pg_roles
                WHERE rolname = 'athera_app';
                """, migConn);
            await using var reader = await roleCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.False(reader.GetBoolean(0)); // NOSUPERUSER
            Assert.False(reader.GetBoolean(1)); // NOBYPASSRLS
            await reader.CloseAsync();

            // Verify athera_app owns ZERO tables
            await using var ownCmd = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_tables
                WHERE tableowner = 'athera_app';
                """, migConn);
            Assert.Equal(0L, Convert.ToInt64(await ownCmd.ExecuteScalarAsync()));

            // Verify EVERY tenant-scoped table across all business schemas has ENABLE + FORCE ROW LEVEL SECURITY
            await using var rlsCheckCmd = new NpgsqlCommand("""
                SELECT n.nspname || '.' || c.relname
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
                WHERE c.relkind = 'r'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'public')
                  AND (NOT c.relrowsecurity OR NOT c.relforcerowsecurity)
                ORDER BY 1;
                """, migConn);
            var unprotected = new List<string>();
            await using var rlsReader = await rlsCheckCmd.ExecuteReaderAsync();
            while (await rlsReader.ReadAsync())
            {
                unprotected.Add(rlsReader.GetString(0));
            }
            await rlsReader.CloseAsync();
            Assert.Empty(unprotected);

            // Confirm at least 55 tenant-scoped tables are actively protected by forced RLS
            await using var protectedCountCmd = new NpgsqlCommand("""
                SELECT COUNT(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
                WHERE c.relkind = 'r'
                  AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'public')
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """, migConn);
            Assert.True(Convert.ToInt64(await protectedCountCmd.ExecuteScalarAsync()) >= 55L);
        }

        // 2. Hostile Cross-Tenant, Unauthenticated, and Unassigned API Fuzz Matrix across all major modules
        using var factory = fx.CreateApiFactory();
        using var anonClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignEmail, world.Password);
        using var unassignedClient = await SignInAndGetCsrfAsync(factory, world.UnassignedEmail, world.Password);

        var entityBase = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}";
        var endpoints = new[]
        {
            $"{entityBase}/partners",
            $"{entityBase}/catalog/items",
            $"{entityBase}/procurement/purchase-orders",
            $"{entityBase}/receiving/receipts",
            $"{entityBase}/inventory/overview",
            $"{entityBase}/payables/overview",
            $"{entityBase}/payments/overview",
            $"{entityBase}/sales-orders",
            $"{entityBase}/fulfillment/overview",
            $"{entityBase}/billing/overview",
            $"{entityBase}/receivables/overview",
            $"{entityBase}/reports/overview",
            $"{entityBase}/documents/overview",
            $"{entityBase}/admin/overview"
        };

        foreach (var url in endpoints)
        {
            // Unauthenticated must be 401
            var anonRes = await anonClient.GetAsync(url);
            Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);

            // Foreign tenant caller must be denied (403 or 404, NEVER 200)
            var foreignRes = await foreignClient.GetAsync(url);
            Assert.True(
                foreignRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"Expected 403/404 for foreign tenant on {url}, got {foreignRes.StatusCode}");

            // Unassigned user must be denied (403 or 404, NEVER 200)
            var unassignedRes = await unassignedClient.GetAsync(url);
            Assert.True(
                unassignedRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"Expected 403/404 for unassigned user on {url}, got {unassignedRes.StatusCode}");
        }
    }

    [Fact]
    public async Task S18_CONCURRENCY_01_ConcurrentJournalPostingIdempotencyAndStockReservationSoak()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedRcWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var ctrlClient = await SignInAndGetCsrfAsync(factory, world.ControllerEmail, world.Password);
        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}";

        // Part A: 12 concurrent journal posts (6 unique keys, each fired twice in parallel to stress idempotency + sequence locking)
        var tasks = new List<Task<HttpResponseMessage>>();
        for (var i = 1; i <= 6; i++)
        {
            var idx = i;
            var sourceKey = $"soak-jrn-{idx:D2}";
            for (var dup = 0; dup < 2; dup++)
            {
                tasks.Add(Task.Run(async () =>
                    await ctrlClient.PostAsJsonAsync($"{baseUrl}/finance/journals", new PostJournalRequest(
                        new DateOnly(2026, 11, 15),
                        "IDR",
                        $"Concurrent soak journal #{idx}",
                        "MANUAL_GL",
                        "MANUAL_JOURNAL",
                        sourceKey,
                        sourceKey,
                        [
                            new JournalLineInputDto(world.CashAccountId, "Kas soak", 10_000m * idx, 0m, null, null, null),
                            new JournalLineInputDto(world.RevenueAccountId, "Pendapatan soak", 0m, 10_000m * idx, null, null, null)
                        ]))));
            }
        }

        var responses = await Task.WhenAll(tasks);
        // Every call must either succeed (201/200 idempotent replay) or cleanly conflict (409) without any 500
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected concurrent status: {r.StatusCode}"));

        // Verify Trial Balance is balanced and exact journal count matches 6 new unique entries (zero duplicates!)
        var reports = await ctrlClient.GetFromJsonAsync<AccountingReportsOverviewResponse>(
            $"{baseUrl}/reports/overview?periodId={world.PeriodNovId}", JsonOptions);
        Assert.NotNull(reports);
        Assert.True(reports!.TrialBalance.IsBalanced);
        Assert.True(reports.BalanceSheet.IsBalanced);
        Assert.Equal(reports.TrialBalance.TotalEndingDebit, reports.TrialBalance.TotalEndingCredit);
        // Opening seed (1,000,000) + 6 unique soak entries (10k+20k+30k+40k+50k+60k = 210,000) = 1,210,000
        Assert.Equal(1_210_000m, reports.TrialBalance.TotalEndingDebit);

        // Part B: 8 concurrent reservations of 4 PCS each against 10 PCS available stock -> at most 2 can succeed (8 <= 10), zero over-reservation!
        var resTasks = Enumerable.Range(1, 8).Select(idx => Task.Run(async () =>
            await ctrlClient.PostAsJsonAsync($"{baseUrl}/inventory/reservations", new CreateStockReservationRequest(
                world.ItemId,
                world.MainLocationId,
                null,
                "MANUAL_HOLD",
                null,
                $"SOAK-HOLD-{idx:D2}",
                null,
                4m,
                null,
                $"soak-res-key-{idx:D2}",
                $"Concurrent reservation #{idx}")))).ToList();

        var resResponses = await Task.WhenAll(resTasks);
        var succeededRes = resResponses.Count(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
        var rejectedRes = resResponses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, succeededRes); // 2 * 4 = 8 <= 10 PCS; 3rd would require 12 > 10 PCS!
        Assert.Equal(6, rejectedRes);
        Assert.All(resResponses, r => Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode));
    }

    [Fact]
    public async Task S18_SEC_02_HostileMutationMatrixNeverWritesOrLeaks()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedRcWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var anonClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignEmail, world.Password);
        using var unassignedClient = await SignInAndGetCsrfAsync(factory, world.UnassignedEmail, world.Password);
        using var readerClient = await SignInAndGetCsrfAsync(factory, world.ReaderEmail, world.Password);

        var entityBase = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}";

        async Task AssertNoLeakAsync(HttpResponseMessage res, string route, string actor)
        {
            var body = await res.Content.ReadAsStringAsync();
            Assert.False(body.Contains("NUSA", StringComparison.Ordinal) || body.Contains("Nusantara", StringComparison.Ordinal),
                $"Response body leaked tenant data on {route} for {actor}: {body}");
        }

        var journalBody = new PostJournalRequest(
            new DateOnly(2026, 11, 15),
            "IDR",
            "Hostile journal attempt",
            "MANUAL_GL",
            "MANUAL_JOURNAL",
            "sec02-jrn",
            "sec02-jrn",
            [
                new JournalLineInputDto(world.CashAccountId, "Dr", 10_000m, 0m, null, null, null),
                new JournalLineInputDto(world.RevenueAccountId, "Cr", 0m, 10_000m, null, null, null)
            ]);
        var reservationBody = new CreateStockReservationRequest(
            world.ItemId,
            world.MainLocationId,
            null,
            "MANUAL_HOLD",
            null,
            "SEC02-HOLD",
            null,
            1m,
            null,
            "sec02-res",
            "Hostile reservation attempt");

        var validTargets = new (string Route, object Body)[]
        {
            ($"{entityBase}/finance/journals", journalBody),
            ($"{entityBase}/inventory/reservations", reservationBody)
        };

        foreach (var (route, body) in validTargets)
        {
            var anonRes = await anonClient.PostAsJsonAsync(route, body, JsonOptions);
            Assert.True(
                anonRes.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Expected 401/403 for anonymous on {route}, got {anonRes.StatusCode}");
            await AssertNoLeakAsync(anonRes, route, "anonymous");

            var foreignRes = await foreignClient.PostAsJsonAsync(route, body, JsonOptions);
            Assert.True(
                foreignRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"Expected 403/404 for foreign tenant on {route}, got {foreignRes.StatusCode}");
            await AssertNoLeakAsync(foreignRes, route, "foreign");

            var unassignedRes = await unassignedClient.PostAsJsonAsync(route, body, JsonOptions);
            Assert.True(
                unassignedRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                $"Expected 403/404 for unassigned user on {route}, got {unassignedRes.StatusCode}");
            await AssertNoLeakAsync(unassignedRes, route, "unassigned");

            var readerRes = await readerClient.PostAsJsonAsync(route, body, JsonOptions);
            Assert.True(
                readerRes.StatusCode == HttpStatusCode.Forbidden,
                $"Expected exactly 403 for read-only user on {route}, got {readerRes.StatusCode}");
        }

        var emptyBodyTargets = new[]
        {
            $"{entityBase}/partners",
            $"{entityBase}/catalog/items",
            $"{entityBase}/procurement/purchase-orders",
            $"{entityBase}/receiving/receipts",
            $"{entityBase}/payables/bills",
            $"{entityBase}/payments",
            $"{entityBase}/sales-orders",
            $"{entityBase}/fulfillment/reservations",
            $"{entityBase}/billing/invoices",
            $"{entityBase}/receivables/receipts",
            $"{entityBase}/documents/upload",
            $"{entityBase}/admin/invitations"
        };

        foreach (var route in emptyBodyTargets)
        {
            foreach (var (client, actor) in new (HttpClient Client, string Actor)[]
            {
                (anonClient, "anonymous"), (foreignClient, "foreign"), (unassignedClient, "unassigned")
            })
            {
                var res = await client.PostAsJsonAsync(route, new { }, JsonOptions);
                Assert.True(
                    res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
                    $"Hostile {actor} on {route} returned {(int)res.StatusCode} {res.StatusCode}");
                await AssertNoLeakAsync(res, route, actor);
            }
        }

        // Hostile matrix wrote nothing on tenant A
        await using (var db = fx.CreateMigratorDbContext())
        {
            Assert.Equal(1, await db.JournalEntries.LongCountAsync(
                j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id));
            Assert.Equal(0, await db.StockReservations.LongCountAsync(
                r => r.TenantId == world.TenantAId && r.LegalEntityId == world.EntityA1Id));
            Assert.Equal(0, await db.Partners.LongCountAsync(p => p.TenantId == world.TenantAId));
            Assert.Equal(0, await db.PurchaseOrders.LongCountAsync(p => p.TenantId == world.TenantAId));
        }
    }

    internal sealed record RcWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ControllerId,
        string ControllerEmail,
        string ForeignEmail,
        string UnassignedEmail,
        string ReaderEmail,
        Guid PeriodNovId,
        Guid CashAccountId,
        Guid RevenueAccountId,
        Guid ItemId,
        Guid MainLocationId,
        string Password);

    internal static async Task<RcWorld> SeedRcWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (ctrlId, ctrlEmail, _) = await fx.CreateTestUserAsync("ctrl.s18@test.local", "Budi RC", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.s18@test.local", "Foreign RC", pw);
        var (unassignedId, unassignedEmail, _) = await fx.CreateTestUserAsync("unassigned.s18@test.local", "Unassigned RC", pw);
        var (readerId, readerEmail, _) = await fx.CreateTestUserAsync("reader.s18@test.local", "Reader RC", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
        var tenantA = Guid.NewGuid();
        var entityA1 = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var entityB1 = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantA, "NUSA", "PT Nusantara RC", now),
            Tenant.Create(tenantB, "SMDR", "CV Samudra RC", now));
        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1, tenantA, "NUSA-JKT", "PT Nusantara Jakarta RC", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1, tenantB, "SMDR-BDG", "CV Samudra Bandung RC", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantA, ctrlId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantA, readerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantB, foreignId, now));
        _ = unassignedId;

        foreach (var p in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage,
            PermissionCodes.FinanceJournalPost, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantA, entityA1, ctrlId, p, now, null, now));
        }
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantB, entityB1, foreignId, PermissionCodes.FinanceRead, now, null, now));
        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.InventoryRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantA, entityA1, readerId, p, now, null, now));
        }

        var cash = GlAccount.Create(Guid.NewGuid(), tenantA, entityA1, "1110", "Kas & Bank", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var equity = GlAccount.Create(Guid.NewGuid(), tenantA, entityA1, "3100", "Modal Disetor", GlAccountTypes.Equity, GlAccountCategories.EquityCapital, null, true, false, now);
        var rev = GlAccount.Create(Guid.NewGuid(), tenantA, entityA1, "4100", "Pendapatan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, null, true, false, now);
        db.GlAccounts.AddRange(cash, equity, rev);

        var nov = FinancialPeriod.Create(Guid.NewGuid(), tenantA, entityA1, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now);
        db.FinancialPeriods.Add(nov);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantA, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);
        var item = Item.Create(Guid.NewGuid(), tenantA, "ITM-SOAK-01", "Barang Uji Konkurensi", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Items.Add(item);
        await db.SaveChangesAsync();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var j1 = await GlPostingKernel.PostJournalAsync(
                db,
                new GlPostJournalCommand(
                    tenantA, entityA1, "IDR", new DateOnly(2026, 11, 1), "IDR", "Modal awal soak",
                    "FINANCE", "MANUAL_JOURNAL", "SEED-S18", "s18-seed-1",
                    [new GlJournalLineInput(cash.AccountId, "Kas", 1_000_000m, 0m), new GlJournalLineInput(equity.AccountId, "Modal", 0m, 1_000_000m)],
                    ctrlId, now),
                CancellationToken.None);
            Assert.True(j1.Succeeded);

            var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantA, entityA1, now, CancellationToken.None);
            var mainLoc = layout.LocationsByCode["LOC-MAIN"];
            var pos = StockPosition.CreateInitial(Guid.NewGuid(), tenantA, entityA1, item.ItemId, pcs.UomId, now);
            pos.ApplyInboundReceipt(10m, StockStatuses.Unrestricted, now);
            db.StockPositions.Add(pos);

            var bal = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantA, entityA1, pos.StockPositionId, item.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, mainLoc.LocationId, null, StockStatuses.Unrestricted, now);
            bal.AdjustOnHand(10m, now);
            db.StockLocationBalances.Add(bal);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return new RcWorld(
                tenantA, entityA1, tenantB, entityB1, ctrlId, ctrlEmail, foreignEmail, unassignedEmail, readerEmail,
                nov.PeriodId, cash.AccountId, rev.AccountId, item.ItemId, mainLoc.LocationId, pw);
        }
    }

    internal static async Task<HttpClient> SignInAndGetCsrfAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var csrfRes = await client.GetAsync("/api/auth/csrf");
        csrfRes.EnsureSuccessStatusCode();
        var csrfPayload = await csrfRes.Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);

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
}
