using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;

namespace Orphac.Athera.IntegrationTests;

public sealed class S16ReportsPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S16SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid ControllerId,
        string ControllerEmail,
        Guid NonFinanceId,
        string NonFinanceEmail,
        Guid ForeignId,
        string ForeignEmail,
        Guid PeriodNovId,
        Guid CashAccountId,
        Guid EquityAccountId,
        Guid RevenueAccountId,
        string Password);

    [Fact]
    public async Task S16_PG_01_ForcedRlsIsolationAndAppendOnlyPeriodCloseReviewImmutability()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS16WorldAsync(fx);

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        await using (var rlsCmd = appConn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'gl'
                  AND c.relname = 'period_close_reviews'
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(1L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        Guid reviewId;
        await using (var db = fx.CreateMigratorDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var res = await AccountingReportsService.ClosePeriodAsync(
                db, world.TenantAId, world.EntityA1Id, world.PeriodNovId, "PG01 close", true, world.ControllerId, DateTimeOffset.UtcNow, CancellationToken.None);
            Assert.True(res.Succeeded);
            reviewId = res.Review!.CloseReviewId;
            await tx.CommitAsync();
        }

        // Missing GUC & Tenant B see 0 rows
        await using (var cmdNoCtx = new NpgsqlCommand("SELECT COUNT(*) FROM gl.period_close_reviews", appConn))
        {
            Assert.Equal(0L, (long)(await cmdNoCtx.ExecuteScalarAsync())!);
        }

        await using (var txB = await appConn.BeginTransactionAsync())
        {
            await using (var setB = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txB))
            {
                setB.Parameters.AddWithValue("tid", world.TenantBId.ToString("D"));
                await setB.ExecuteNonQueryAsync();
            }

            await using (var countB = new NpgsqlCommand("SELECT COUNT(*) FROM gl.period_close_reviews", appConn, txB))
            {
                Assert.Equal(0L, (long)(await countB.ExecuteScalarAsync())!);
            }

            await txB.RollbackAsync();
        }

        // athera_app lacks UPDATE privilege (42501); athera_migrator hits immutability trigger (55000)
        await using (var txA = await appConn.BeginTransactionAsync())
        {
            await using (var setA = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txA))
            {
                setA.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
                await setA.ExecuteNonQueryAsync();
            }

            var pgExApp = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand("UPDATE gl.period_close_reviews SET reason = 'tampered' WHERE close_review_id = @id", appConn, txA);
                upd.Parameters.AddWithValue("id", reviewId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("42501", pgExApp.SqlState);
            await txA.RollbackAsync();
        }

        await using var migConn = new NpgsqlConnection(fx.MigratorConnectionString);
        await migConn.OpenAsync();
        await using (var txMig = await migConn.BeginTransactionAsync())
        {
            var pgExMig = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand("UPDATE gl.period_close_reviews SET reason = 'tampered' WHERE close_review_id = @id", migConn, txMig);
                upd.Parameters.AddWithValue("id", reviewId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("55000", pgExMig.SqlState);
            await txMig.RollbackAsync();
        }
    }

    [Fact]
    public async Task S16_API_01_TrialBalanceDrilldownCsvExportReconciliationClosePostBlockReopenAndPermanentLock()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS16WorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var finClient = await SignInAndGetCsrfAsync(factory, world.ControllerEmail, world.Password);
        using var nonFinClient = await SignInAndGetCsrfAsync(factory, world.NonFinanceEmail, world.Password);
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignEmail, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/reports";

        // Permission & cross-tenant boundaries
        Assert.Equal(HttpStatusCode.Forbidden, (await nonFinClient.GetAsync($"{baseUrl}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignClient.GetAsync($"{baseUrl}/overview")).StatusCode);

        var sbyOverview = (await finClient.GetFromJsonAsync<AccountingReportsOverviewResponse>(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/reports/overview", JsonOptions))!;
        Assert.False(sbyOverview.CanClosePeriod);
        Assert.False(sbyOverview.CanLockOrReopen);

        // 1. Verify Trial Balance, P&L, Balance Sheet, and 5-Control Reconciliations
        var overview = (await finClient.GetFromJsonAsync<AccountingReportsOverviewResponse>($"{baseUrl}/overview?periodId={world.PeriodNovId}", JsonOptions))!;
        Assert.True(overview.TrialBalance.IsBalanced);
        Assert.Equal(1_500_000m, overview.TrialBalance.TotalEndingDebit);
        Assert.Equal(1_500_000m, overview.TrialBalance.TotalEndingCredit);
        Assert.Equal(500_000m, overview.ProfitAndLoss.TotalRevenue);
        Assert.Equal(500_000m, overview.ProfitAndLoss.NetIncome);
        Assert.True(overview.BalanceSheet.IsBalanced);
        Assert.Equal(1_500_000m, overview.BalanceSheet.TotalAssets);
        Assert.Equal(1_500_000m, overview.BalanceSheet.TotalLiabilitiesAndEquity);
        Assert.All(overview.Reconciliations, r => Assert.Equal("RECONCILED", r.Status));

        // 2. Source-linked GL Account Detail drilldown on Cash 1110
        var drilldown = (await finClient.GetFromJsonAsync<AccountLedgerDrilldownResponse>($"{baseUrl}/accounts/{world.CashAccountId}/ledger", JsonOptions))!;
        Assert.Equal("1110", drilldown.AccountCode);
        Assert.Equal(2, drilldown.Entries.Count);
        Assert.Equal(1_500_000m, drilldown.Entries[^1].RunningBalance);

        // 3. Deterministic CSV export matches API Trial Balance totals
        var csv = await finClient.GetStringAsync($"{baseUrl}/trial-balance/export.csv?periodId={world.PeriodNovId}");
        Assert.Contains("TOTAL,\"Total Neraca Saldo (2026-11)\",SUMMARY,0.00,0.00,1500000.00,1500000.00,1500000.00,1500000.00", csv);

        // 4. Close period 2026-11 -> posting a new journal in 2026-11 is denied (409)
        var closeResp = await finClient.PostAsJsonAsync($"{baseUrl}/periods/{world.PeriodNovId}/close", new ClosePeriodRequest("Close Nov 2026", true));
        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);

        var postClosedJournal = await finClient.PostAsJsonAsync(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/finance/journals",
            new PostJournalRequest(
                new DateOnly(2026, 11, 20),
                "IDR",
                "Attempt post to closed period",
                "FINANCE",
                "MANUAL_JOURNAL",
                "S16-CLOSED-1",
                "s16-closed-block",
                [
                    new JournalLineInputDto(world.CashAccountId, "Dr", 10_000m, 0m, null, null, null),
                    new JournalLineInputDto(world.RevenueAccountId, "Cr", 0m, 10_000m, null, null, null)
                ]));
        Assert.Equal(HttpStatusCode.Conflict, postClosedJournal.StatusCode);

        // 5. Controlled reopen with reason succeeds -> re-close -> permanent lock -> reopen blocked (409)
        var reopenResp = await finClient.PostAsJsonAsync($"{baseUrl}/periods/{world.PeriodNovId}/reopen", new ReopenPeriodRequest("Late audit adjustment"));
        Assert.Equal(HttpStatusCode.OK, reopenResp.StatusCode);

        var recloseResp = await finClient.PostAsJsonAsync($"{baseUrl}/periods/{world.PeriodNovId}/close", new ClosePeriodRequest("Final close", true));
        Assert.Equal(HttpStatusCode.OK, recloseResp.StatusCode);

        var lockResp = await finClient.PostAsJsonAsync($"{baseUrl}/periods/{world.PeriodNovId}/lock", new LockPeriodRequest("Permanent audit lock"));
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        var reopenLocked = await finClient.PostAsJsonAsync($"{baseUrl}/periods/{world.PeriodNovId}/reopen", new ReopenPeriodRequest("Try bypass lock"));
        Assert.Equal(HttpStatusCode.Conflict, reopenLocked.StatusCode);

        var finalOverview = (await finClient.GetFromJsonAsync<AccountingReportsOverviewResponse>($"{baseUrl}/overview?periodId={world.PeriodNovId}", JsonOptions))!;
        Assert.Equal(4, finalOverview.CloseReviews.Count);
        Assert.Equal("LOCKED", finalOverview.Periods.Single(p => p.PeriodId == world.PeriodNovId).Status);
    }

    private static async Task<HttpClient> SignInAndGetCsrfAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("Origin", "http://127.0.0.1:3200");
        var csrfPayload = await (await client.GetAsync("/api/auth/csrf")).Content.ReadFromJsonAsync<CsrfTokenResponse>(JsonOptions);
        using var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new LoginRequest(email, password, false)) };
        loginReq.Headers.Add(csrfPayload!.HeaderName, csrfPayload.RequestToken);
        var loginRes = await client.SendAsync(loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var session = await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session!.CsrfToken);
        return client;
    }

    private static async Task<S16SeedWorld> SeedS16WorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (ctrlId, ctrlEmail, _) = await fx.CreateTestUserAsync("ctrl.a1.s16@test.local", "Budi Controller", pw);
        var (nonFinId, nonFinEmail, _) = await fx.CreateTestUserAsync("proc.a1.s16@test.local", "Rina Proc", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("for.b1.s16@test.local", "Samudra Staff", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-20);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", now),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", now));
        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, ctrlId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, nonFinId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, ctrlId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, ctrlId, p, now, null, now));
        }

        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, nonFinId, PermissionCodes.ProcurementRead, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, PermissionCodes.FinanceRead, now, null, now));

        var cash = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1110", "Kas & Bank Operasional", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var clearing = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1120", "Kliring Pembayaran Keluar", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, null, true, false, now);
        var ar = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1200", "Piutang Usaha", GlAccountTypes.Asset, GlAccountCategories.AccountsReceivable, null, true, true, now);
        var inv = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var grni = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2050", "Akrual GRNI", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        var ap = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2100", "Hutang Usaha", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, null, true, true, now);
        var equity = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "3100", "Modal Disetor", GlAccountTypes.Equity, GlAccountCategories.EquityCapital, null, true, false, now);
        var rev = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "4100", "Pendapatan Penjualan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, null, true, false, now);
        db.GlAccounts.AddRange(cash, clearing, ar, inv, grni, ap, equity, rev);

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankOutboundClearing, clearing, "1120", ctrlId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ArTradeControl, ar, "1200", ctrlId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, inv, "1300", ctrlId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grni, "2050", ctrlId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ApTradeControl, ap, "2100", ctrlId, now));

        var nov = FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now);
        db.FinancialPeriods.Add(nov);
        await db.SaveChangesAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        var j1 = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                tenantAId, entityA1Id, "IDR", new DateOnly(2026, 11, 1), "IDR", "Setoran modal awal",
                "FINANCE", "MANUAL_JOURNAL", "SEED-1", "s16-j1",
                [new GlJournalLineInput(cash.AccountId, "Kas", 1_000_000m, 0m), new GlJournalLineInput(equity.AccountId, "Modal", 0m, 1_000_000m)],
                ctrlId, now),
            CancellationToken.None);
        Assert.True(j1.Succeeded);

        var j2 = await GlPostingKernel.PostJournalAsync(
            db,
            new GlPostJournalCommand(
                tenantAId, entityA1Id, "IDR", new DateOnly(2026, 11, 10), "IDR", "Pendapatan tunai",
                "FINANCE", "MANUAL_JOURNAL", "SEED-2", "s16-j2",
                [new GlJournalLineInput(cash.AccountId, "Kas", 500_000m, 0m), new GlJournalLineInput(rev.AccountId, "Pendapatan", 0m, 500_000m)],
                ctrlId, now.AddMinutes(1)),
            CancellationToken.None);
        Assert.True(j2.Succeeded);
        await tx.CommitAsync();

        return new S16SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            ctrlId, ctrlEmail, nonFinId, nonFinEmail, foreignId, foreignEmail,
            nov.PeriodId, cash.AccountId, equity.AccountId, rev.AccountId, pw);
    }
}
