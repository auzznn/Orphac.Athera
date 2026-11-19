using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Billing;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.Fulfillment;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Receivables;
using Orphac.Athera.Modules.Sales;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S15ReceivablesPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S15SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid FinanceId,
        string FinanceEmail,
        Guid ProcurementOnlyId,
        string ProcurementOnlyEmail,
        Guid ForeignUserB1Id,
        string ForeignUserB1Email,
        Guid CustomerAId,
        Guid BankAccountA1Id,
        Guid Receivable1Id,
        Guid Receivable2Id,
        string Password);

    [Fact]
    public async Task S15_PG_01_ForcedRlsIsolationReversedReceiptImmutabilityAndDuplicateBankImportGuard()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS15WorldAsync(fx);

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        await using (var rlsCmd = appConn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'ar'
                  AND c.relname IN ('receipt_sequences', 'customer_receipts', 'receipt_allocations')
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(3L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Import inbound CREDIT bank statement, confirm receipt, and reverse it
        Guid reversedReceiptId;
        await using (var db = fx.CreateMigratorDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var csv = "ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-19,CREDIT,250000,TRF-IN-PG01,PT Graha Ritel,Pelunasan tagihan";
            var imp = await BankReconciliationCoordinator.ImportBankStatementAsync(
                db,
                new ImportBankStatementCommand(
                    world.TenantAId, world.EntityA1Id, world.BankAccountA1Id,
                    "STMT-S15-PG01", new DateOnly(2026, 11, 19), 0m, 250_000m, csv, null, world.FinanceId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(imp.Succeeded);

            // Duplicate bank statement import must be rejected (409)
            var dupImp = await BankReconciliationCoordinator.ImportBankStatementAsync(
                db,
                new ImportBankStatementCommand(
                    world.TenantAId, world.EntityA1Id, world.BankAccountA1Id,
                    "STMT-S15-PG01-DUP", new DateOnly(2026, 11, 19), 0m, 250_000m, csv, null, world.FinanceId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.False(dupImp.Succeeded);
            Assert.Equal(409, dupImp.FailureHttpStatus);

            var rct = await CustomerReceiptsCoordinator.ConfirmReceiptAsync(
                db,
                new ConfirmCustomerReceiptCommand(
                    world.TenantAId, world.EntityA1Id, imp.Lines[0].BankStatementLineId, world.CustomerAId,
                    new DateOnly(2026, 11, 19), "pg01-rct-1", null,
                    [new ReceiptAllocationInput(world.Receivable1Id, 150_000m)],
                    world.FinanceId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(rct.Succeeded);

            var rev = await CustomerReceiptsCoordinator.ReverseReceiptAsync(
                db,
                new ReverseCustomerReceiptCommand(
                    world.TenantAId, world.EntityA1Id, rct.Value!.Receipt.ReceiptId,
                    new DateOnly(2026, 11, 19), "Bank transfer recalled", world.FinanceId, DateTimeOffset.UtcNow),
                CancellationToken.None);
            Assert.True(rev.Succeeded);
            reversedReceiptId = rev.Value!.Receipt.ReceiptId;
            await tx.CommitAsync();
        }

        // Verify Tenant B sees 0 receipts; missing GUC sees 0 receipts
        await using (var cmdNoCtx = new NpgsqlCommand("SELECT COUNT(*) FROM ar.customer_receipts", appConn))
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

            await using (var countB = new NpgsqlCommand("SELECT COUNT(*) FROM ar.customer_receipts", appConn, txB))
            {
                Assert.Equal(0L, (long)(await countB.ExecuteScalarAsync())!);
            }

            await txB.RollbackAsync();
        }

        // Verify reversed receipt immutability trigger (SQLSTATE 55000) under athera_app
        await using (var txA = await appConn.BeginTransactionAsync())
        {
            await using (var setA = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, true)", appConn, txA))
            {
                setA.Parameters.AddWithValue("tid", world.TenantAId.ToString("D"));
                await setA.ExecuteNonQueryAsync();
            }

            var pgEx = await Assert.ThrowsAsync<PostgresException>(async () =>
            {
                await using var upd = new NpgsqlCommand(
                    "UPDATE ar.customer_receipts SET notes = 'tampered' WHERE receipt_id = @id", appConn, txA);
                upd.Parameters.AddWithValue("id", reversedReceiptId);
                await upd.ExecuteNonQueryAsync();
            });
            Assert.Equal("55000", pgEx.SqlState);
            await txA.RollbackAsync();
        }
    }

    [Fact]
    public async Task S15_API_01_ImportInboundCreditConfirmMultiInvoiceAllocationUnappliedGlProofAgingAndReversal()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS15WorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var finClient = await SignInAndGetCsrfAsync(factory, world.FinanceEmail, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/receivables";

        // Seeded open AR: Receivable1 = 333,000 (due 2026-10-15 -> 35 days past due as of 2026-11-19 -> 31-60 bucket)
        //                 Receivable2 = 333,000 (due 2026-12-19 -> Current bucket)
        // Total Open AR = 666,000
        var initialOverview = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>($"{baseUrl}/overview?asOfDate=2026-11-19", JsonOptions))!;
        Assert.Equal(666_000m, initialOverview.Aging.TotalOpenReceivables);
        Assert.Equal(333_000m, initialOverview.Aging.CurrentTotal);
        Assert.Equal(333_000m, initialOverview.Aging.Days31To60Total);
        Assert.True(initialOverview.Aging.IsGlReconciled);

        // 1. Import inbound CREDIT bank statement line of 500,000
        var csv = "ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-19,CREDIT,500000,TRF-CUST-500K,PT Graha Ritel Nusantara,Pembayaran 2 faktur";
        var impResp = await finClient.PostAsJsonAsync($"{baseUrl}/bank-statements/import", new ImportInboundBankStatementRequest(
            world.BankAccountA1Id, "STMT-IN-001", new DateOnly(2026, 11, 19), 0m, 500_000m, csv));
        Assert.Equal(HttpStatusCode.Created, impResp.StatusCode);

        // Duplicate import rejected (409)
        var dupResp = await finClient.PostAsJsonAsync($"{baseUrl}/bank-statements/import", new ImportInboundBankStatementRequest(
            world.BankAccountA1Id, "STMT-IN-001-DUP", new DateOnly(2026, 11, 19), 0m, 500_000m, csv));
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);

        var afterImp = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>($"{baseUrl}/overview?asOfDate=2026-11-19", JsonOptions))!;
        var bankLine = Assert.Single(afterImp.InboundBankLines);
        Assert.Equal("UNMATCHED", bankLine.Status);

        // 2. Confirm customer receipt of 500,000 with initial partial allocation across 2 invoices:
        //    - 333,000 to Receivable1 (full settlement)
        //    - 100,000 to Receivable2 (partial settlement -> 233,000 open)
        //    - 67,000 explicitly UNAPPLIED!
        var confirmResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts", new ConfirmCustomerReceiptRequest(
            bankLine.BankStatementLineId,
            world.CustomerAId,
            new DateOnly(2026, 11, 19),
            "api-rct-500k",
            "Transfer masuk sebagian alokasi",
            [
                new ReceiptAllocationLineRequest(world.Receivable1Id, 333_000m),
                new ReceiptAllocationLineRequest(world.Receivable2Id, 100_000m)
            ]));
        Assert.Equal(HttpStatusCode.Created, confirmResp.StatusCode);
        var rct = (await confirmResp.Content.ReadFromJsonAsync<CustomerReceiptDto>(JsonOptions))!;
        Assert.Equal("PARTIALLY_ALLOCATED", rct.Status);
        Assert.Equal(500_000m, rct.ReceiptAmount);
        Assert.Equal(433_000m, rct.AllocatedAmount);
        Assert.Equal(67_000m, rct.UnappliedAmount);
        Assert.Equal(2, rct.Allocations.Count);

        // Recognizing same bank line a second time is rejected (409)
        var doubleRecog = await finClient.PostAsJsonAsync($"{baseUrl}/receipts", new ConfirmCustomerReceiptRequest(
            bankLine.BankStatementLineId, world.CustomerAId, new DateOnly(2026, 11, 19), "api-rct-double", null, null));
        Assert.Equal(HttpStatusCode.Conflict, doubleRecog.StatusCode);

        // Verify GL 1200 Control reconciliation: Open AR (233,000) - Unapplied (67,000) == Net AR (166,000) == GL 1200 (166,000)
        var afterConfirm = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>($"{baseUrl}/overview?asOfDate=2026-11-19", JsonOptions))!;
        Assert.Equal(233_000m, afterConfirm.Aging.TotalOpenReceivables);
        Assert.Equal(67_000m, afterConfirm.Aging.TotalUnappliedReceipts);
        Assert.Equal(166_000m, afterConfirm.Aging.NetArSubledger);
        Assert.Equal(166_000m, afterConfirm.Aging.GlArControlBalance);
        Assert.True(afterConfirm.Aging.IsGlReconciled);

        // 3. Allocate remaining 67,000 to Receivable2 -> receipt becomes ALLOCATED (0 unapplied), Receivable2 open drops to 166,000
        var allocResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts/{rct.ReceiptId}/allocate", new AllocateCustomerReceiptRequest(
            [new ReceiptAllocationLineRequest(world.Receivable2Id, 67_000m)]));
        Assert.Equal(HttpStatusCode.OK, allocResp.StatusCode);
        var rctAlloc = (await allocResp.Content.ReadFromJsonAsync<CustomerReceiptDto>(JsonOptions))!;
        Assert.Equal("ALLOCATED", rctAlloc.Status);
        Assert.Equal(0m, rctAlloc.UnappliedAmount);

        // 4. Reverse receipt -> restores both receivables to 333,000 + 333,000 = 666,000, unmatches bank line, posts Dr 1200 / Cr 1110, GL still reconciled!
        var revResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts/{rct.ReceiptId}/reverse", new ReverseCustomerReceiptRequest(
            new DateOnly(2026, 11, 19), "Bank bounce / kliring ditolak"));
        Assert.Equal(HttpStatusCode.OK, revResp.StatusCode);
        var rctRev = (await revResp.Content.ReadFromJsonAsync<CustomerReceiptDto>(JsonOptions))!;
        Assert.Equal("REVERSED", rctRev.Status);
        Assert.NotNull(rctRev.ReversalGlJournalNumber);

        var afterRev = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>($"{baseUrl}/overview?asOfDate=2026-11-19", JsonOptions))!;
        Assert.Equal(666_000m, afterRev.Aging.TotalOpenReceivables);
        Assert.Equal(0m, afterRev.Aging.TotalUnappliedReceipts);
        Assert.Equal(666_000m, afterRev.Aging.GlArControlBalance);
        Assert.True(afterRev.Aging.IsGlReconciled);
        Assert.Equal("UNMATCHED", Assert.Single(afterRev.InboundBankLines).Status);
    }

    [Fact]
    public async Task S15_API_02_ClosedPeriodOverAllocationPermissionsAndCrossTenantDenial()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedS15WorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var finClient = await SignInAndGetCsrfAsync(factory, world.FinanceEmail, world.Password);
        using var procClient = await SignInAndGetCsrfAsync(factory, world.ProcurementOnlyEmail, world.Password);
        using var foreignClient = await SignInAndGetCsrfAsync(factory, world.ForeignUserB1Email, world.Password);

        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}/receivables";

        Assert.Equal(HttpStatusCode.Forbidden, (await procClient.GetAsync($"{baseUrl}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignClient.GetAsync($"{baseUrl}/overview")).StatusCode);

        var sbyOverview = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>(
            $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA2Id}/receivables/overview", JsonOptions))!;
        Assert.False(sbyOverview.CanManage);

        // Import inbound CREDIT line of 400,000
        var csv = "ValueDate,Direction,Amount,BankReference,Counterparty,Description\n2026-11-19,CREDIT,400000,TRF-NEG-400K,PT Graha Ritel,Uji batas";
        await finClient.PostAsJsonAsync($"{baseUrl}/bank-statements/import", new ImportInboundBankStatementRequest(
            world.BankAccountA1Id, "STMT-NEG-01", new DateOnly(2026, 11, 19), 0m, 400_000m, csv));
        var ov = (await finClient.GetFromJsonAsync<ReceivablesOverviewResponse>($"{baseUrl}/overview", JsonOptions))!;
        var lineId = ov.InboundBankLines[0].BankStatementLineId;

        // Closed period (2026-10-15) blocked -> 409
        var closedResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts", new ConfirmCustomerReceiptRequest(
            lineId, world.CustomerAId, new DateOnly(2026, 10, 15), "neg-closed", null, null));
        Assert.Equal(HttpStatusCode.Conflict, closedResp.StatusCode);

        // Over-allocation on single invoice (400,000 > 333,000 open) blocked -> 409
        var overInvResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts", new ConfirmCustomerReceiptRequest(
            lineId, world.CustomerAId, new DateOnly(2026, 11, 19), "neg-over-inv", null,
            [new ReceiptAllocationLineRequest(world.Receivable1Id, 400_000m)]));
        Assert.Equal(HttpStatusCode.Conflict, overInvResp.StatusCode);

        // Confirm valid unapplied receipt of 400,000, then attempt over-allocation (> 400,000) -> 409
        var okResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts", new ConfirmCustomerReceiptRequest(
            lineId, world.CustomerAId, new DateOnly(2026, 11, 19), "neg-ok-unap", null, null));
        Assert.Equal(HttpStatusCode.Created, okResp.StatusCode);
        var rct = (await okResp.Content.ReadFromJsonAsync<CustomerReceiptDto>(JsonOptions))!;

        var overRctResp = await finClient.PostAsJsonAsync($"{baseUrl}/receipts/{rct.ReceiptId}/allocate", new AllocateCustomerReceiptRequest(
            [
                new ReceiptAllocationLineRequest(world.Receivable1Id, 300_000m),
                new ReceiptAllocationLineRequest(world.Receivable2Id, 200_000m)
            ]));
        Assert.Equal(HttpStatusCode.Conflict, overRctResp.StatusCode);
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

    private static async Task<S15SeedWorld> SeedS15WorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (finId, finEmail, _) = await fx.CreateTestUserAsync("fin.a1.s15@test.local", "Budi Finance", pw);
        var (procId, procEmail, _) = await fx.CreateTestUserAsync("proc.a1.s15@test.local", "Rina Proc", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("for.b1.s15@test.local", "Samudra Staff", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-30);
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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, finId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, procId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceJournalPost, PermissionCodes.SalesRead, PermissionCodes.SalesManage, PermissionCodes.SalesApprove, PermissionCodes.InventoryRead, PermissionCodes.InventoryManage })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, finId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.SalesRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, finId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, procId, p, now, null, now));
        }

        foreach (var p in new[] { PermissionCodes.WorkspaceView, PermissionCodes.FinanceRead, PermissionCodes.FinanceJournalPost })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, p, now, null, now));
        }

        db.ApprovalPolicies.Add(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, SalesPolicyDefaults.SalesOrderApprovalPolicyCode, "SO Approval", true, false, now));

        var bankAcctGl = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1110", "Kas & Bank Operasional", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var clearingGl = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1120", "Kliring Pembayaran Keluar", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, null, true, false, now);
        var arAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1200", "Piutang Usaha", GlAccountTypes.Asset, GlAccountCategories.AccountsReceivable, null, true, true, now);
        var invAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnOutAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2210", "PPN Keluaran", GlAccountTypes.Liability, GlAccountCategories.TaxPayable, null, true, false, now);
        var revAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "4100", "Pendapatan Penjualan", GlAccountTypes.Revenue, GlAccountCategories.SalesRevenue, null, true, false, now);
        var cogsAcct = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "HPP", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, false, now);
        db.GlAccounts.AddRange(bankAcctGl, clearingGl, arAcct, invAcct, ppnOutAcct, revAcct, cogsAcct);

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankMainOperating, bankAcctGl, "Bank 1110", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankOutboundClearing, clearingGl, "Clearing 1120", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ArTradeControl, arAcct, "AR 1200", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcct, "Inv 1300", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnOutputTax, ppnOutAcct, "PPN Out 2210", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.SalesRevenueDefault, revAcct, "Sales Rev 4100", finId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcct, "COGS 5100", finId, now));

        // Open Sep & Nov (so we can post an overdue invoice due Oct 15!), Closed Oct
        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-09", 2026, 9, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), FinancialPeriodStatuses.Open, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        var bankAccount = BankAccount.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "BCA-OPS-01", "Bank Central Asia", "KCU Sudirman", "123-456-7890", "PT Nusantara Distribusi Jakarta", "IDR",
            bankAcctGl.AccountId, clearingGl.AccountId, finId, now);
        db.BankAccounts.Add(bankAccount);

        var ppnOut11 = TaxCode.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "PPN-OUT-11", "PPN Keluaran 11%",
            TaxFamilies.Ppn, TaxDirections.Output, TaxTreatmentClasses.StandardTaxable,
            11m, 1, 1, true, false, ppnOutAcct.AccountId, new DateOnly(2025, 1, 1), null,
            TaxReviewStatuses.UnreviewedConfigurable, "OD-07", now);
        db.TaxCodes.Add(ppnOut11);

        var cust = Partner.Create(Guid.NewGuid(), tenantAId, "PT-CUST-001", "PT Graha Ritel Nusantara", "Graha Ritel", false, true, "01.777.332.1-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        var itemValve = Item.Create(Guid.NewGuid(), tenantAId, "ITM-VALVE-01", "Katup Industri PN16", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Partners.Add(cust);
        db.UnitsOfMeasure.Add(pcs);
        db.Items.Add(itemValve);
        await db.SaveChangesAsync();

        var layout = await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);
        var locMain = layout.LocationsByCode[InventoryAvailabilityDefaults.DefaultMainLocationCode];

        var pos = StockPosition.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, pcs.UomId, now);
        pos.ApplyInboundReceipt(20m, StockStatuses.Unrestricted, now);
        var pool = ValuationPool.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, itemValve.ItemId, "IDR", pcs.UomId, now);
        pool.ApplyReceiptValuation(20m, 1_000_000m, now);
        var bal = StockLocationBalance.CreateInitial(Guid.NewGuid(), tenantAId, entityA1Id, pos.StockPositionId, itemValve.ItemId, pcs.UomId, layout.DefaultWarehouse.WarehouseId, locMain.LocationId, null, StockStatuses.Unrestricted, now);
        bal.AdjustOnHand(20m, now);
        db.StockPositions.Add(pos);
        db.ValuationPools.Add(pool);
        db.StockLocationBalances.Add(bal);
        await db.SaveChangesAsync();

        // Create & approve SO (10 PCS @ 150,000), reserve/pick/dispatch 4 PCS, then post 2 invoices (2 PCS each = 333,000 each)
        var (approver2Id, _, _) = await fx.CreateTestUserAsync("app2.a1.s15@test.local", "Approver Two", pw);
        db.TenantMemberships.Add(TenantMembership.Create(Guid.NewGuid(), tenantAId, approver2Id, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, approver2Id, PermissionCodes.SalesApprove, now, null, now));
        await db.SaveChangesAsync();

        Guid soId, soLineId, shpLineId, rec1Id, rec2Id;
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var draft = await SalesOrderCoordinator.CreateDraftAsync(
                db,
                new CreateSoDraftCommand(
                    tenantAId, entityA1Id, "IDR", "IDR", cust.PartnerId,
                    new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 20),
                    "Gudang Bekasi Timur", 30, "PO-CUST-S15", false, null,
                    [new SoLineInput("SO-L001", itemValve.ItemId, pcs.UomId, 10m, 150_000m, 0m, ppnOut11.TaxCodeId, null, null)],
                    finId, now),
                CancellationToken.None);
            Assert.True(draft.Succeeded);

            var sub = await SalesOrderCoordinator.SubmitRevisionAsync(db, new SubmitSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder!.SalesOrderId, draft.Revision!.RevisionId, finId, now.AddMinutes(1)), CancellationToken.None);
            Assert.True(sub.Succeeded);
            var dec = await SalesOrderCoordinator.DecideRevisionAsync(db, new DecideSoRevisionCommand(tenantAId, entityA1Id, draft.SalesOrder.SalesOrderId, draft.Revision.RevisionId, ApprovalDecisions.Approved, "Approved", null, approver2Id, now.AddMinutes(2)), CancellationToken.None);
            Assert.True(dec.Succeeded);

            soId = draft.SalesOrder.SalesOrderId;
            soLineId = await db.SalesOrderLines.AsNoTracking().Where(l => l.RevisionId == draft.Revision.RevisionId).Select(l => l.SoLineId).SingleAsync();

            var rsv = await SalesFulfillmentCoordinator.ReserveSalesOrderLineAsync(db, new ReserveSalesOrderLineCommand(tenantAId, entityA1Id, soId, soLineId, locMain.LocationId, null, 4m, "s15-rsv", null, finId, now), CancellationToken.None);
            Assert.True(rsv.Succeeded);
            var pck = await SalesFulfillmentCoordinator.CreatePickOrderAsync(db, new CreatePickOrderCommand(tenantAId, entityA1Id, soId, true, "s15-pck", null, [new PickLineInput(rsv.Reservation!.StockReservationId, 4m, 4m)], finId, now), CancellationToken.None);
            Assert.True(pck.Succeeded);
            var shp = await SalesFulfillmentCoordinator.DispatchShipmentAsync(db, new DispatchSalesShipmentCommand(tenantAId, entityA1Id, "IDR", pck.PickOrder!.PickOrderId, new DateOnly(2026, 9, 15), "SJ-S15-001", "Fleet", "B 1234", "s15-shp", null, null, finId, now), CancellationToken.None);
            Assert.True(shp.Succeeded);
            shpLineId = shp.Lines[0].ShipmentLineId;

            // Invoice 1: Sep 15, Due Oct 15 (35 days past due as of Nov 19 -> 31-60 bucket), 2 PCS = 333,000
            var inv1Draft = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(
                db, new CreateCustomerInvoiceCommand(tenantAId, entityA1Id, soId, ppnOut11.TaxCodeId, new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 15), "FP-S15-001", "s15-inv-1", null, [new BillingLineInput(shpLineId, 2m)], finId, now), CancellationToken.None);
            Assert.True(inv1Draft.Succeeded);
            var inv1Post = await BillingAndReceivablesCoordinator.PostInvoiceAsync(
                db, new PostCustomerInvoiceCommand(tenantAId, entityA1Id, inv1Draft.Value.Invoice.InvoiceId, null, finId, now), CancellationToken.None);
            Assert.True(inv1Post.Succeeded);
            rec1Id = inv1Post.Value!.Receivable.ReceivableId;

            // Invoice 2: Nov 19, Due Dec 19 (Current bucket), 2 PCS = 333,000
            var inv2Draft = await BillingAndReceivablesCoordinator.CreateDraftInvoiceAsync(
                db, new CreateCustomerInvoiceCommand(tenantAId, entityA1Id, soId, ppnOut11.TaxCodeId, new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 19), "FP-S15-002", "s15-inv-2", null, [new BillingLineInput(shpLineId, 2m)], finId, now), CancellationToken.None);
            Assert.True(inv2Draft.Succeeded);
            var inv2Post = await BillingAndReceivablesCoordinator.PostInvoiceAsync(
                db, new PostCustomerInvoiceCommand(tenantAId, entityA1Id, inv2Draft.Value.Invoice.InvoiceId, null, finId, now), CancellationToken.None);
            Assert.True(inv2Post.Succeeded);
            rec2Id = inv2Post.Value!.Receivable.ReceivableId;

            await tx.CommitAsync();
        }

        return new S15SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            finId, finEmail, procId, procEmail, foreignId, foreignEmail,
            cust.PartnerId, bankAccount.BankAccountId, rec1Id, rec2Id, pw);
    }
}
