using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.AccountsPayable;
using Orphac.Athera.Modules.Approval;
using Orphac.Athera.Modules.Cash;
using Orphac.Athera.Modules.Catalog;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Inventory;
using Orphac.Athera.Modules.Partners;
using Orphac.Athera.Modules.Persistence;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Procurement;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S11SupplierSettlementPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S11World(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid BuyerId,
        string BuyerEmail,
        Guid ApClerkId,
        string ApClerkEmail,
        Guid ControllerId,
        string ControllerEmail,
        Guid ForeignId,
        string ForeignEmail,
        Guid SupplierPkpId,
        Guid PcsUomId,
        Guid ItemId,
        Guid PpnInputId,
        Guid JktBankAccountId,
        Guid SbyBankAccountId,
        string Password);

    [Fact]
    public async Task S11_PG_01_ForcedRlsMissingContextAppendOnlyAndImmutabilityTriggers()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var payBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payments";
        var (_, payableId, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 10m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-S11-PG01", "INV-S11-PG01", "pg01");

        // Create proposal, simulate file, submit, four-eyes release, import statement, reconcile
        var propRes = await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-4455", "PT Sumber Makmur PKP",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), "PG01 payment",
            [new PaymentAllocationInputRequest(payableId, 1_110_000m, "Full")]));
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var payment = (await propRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;

        var simRes = await apClerk.PostAsJsonAsync($"{payBase}/{payment.Payment.SupplierPaymentId:D}/simulate-file", new SimulatePaymentInstructionRequest(null));
        Assert.Equal(HttpStatusCode.OK, simRes.StatusCode);
        var simDto = (await simRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        var instId = simDto.Instructions[0].PaymentInstructionId;

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{payBase}/{payment.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{payBase}/{payment.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "REL-PG01", "idem-rel-pg01"))).StatusCode);

        var stmtRes = await controller.PostAsJsonAsync($"{payBase}/bank-statements", new ImportBankStatementRequest(
            world.JktBankAccountId, "STMT-PG01", new DateOnly(2026, 11, 13), 50_000_000m, 48_890_000m,
            "2026-11-13,DEBIT,1110000,TRF-PG01,PT Sumber Makmur,Keluar PG01", null));
        Assert.Equal(HttpStatusCode.Created, stmtRes.StatusCode);

        var ov = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        var stmtId = ov.BankStatements[0].BankStatementId;
        var lineId = ov.StatementLines[0].BankStatementLineId;

        var recRes = await controller.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(
            lineId, payment.Payment.SupplierPaymentId, new DateOnly(2026, 11, 13), 1_110_000m, "idem-rec-pg01"));
        Assert.Equal(HttpStatusCode.OK, recRes.StatusCode);
        var matchDto = (await recRes.Content.ReadFromJsonAsync<BankPaymentMatchDto>(JsonOptions))!;

        await using var appConn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await appConn.OpenAsync();

        // 1. All 11 S11 tables have ENABLE + FORCE ROW LEVEL SECURITY
        await using (var rlsCmd = appConn.CreateCommand())
        {
            rlsCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'ap' AND c.relname IN ('payment_sequences', 'supplier_payments', 'payment_allocations'))
                    OR (n.nspname = 'cash' AND c.relname IN (
                        'bank_accounts', 'instruction_sequences', 'statement_sequences', 'match_sequences',
                        'payment_instructions', 'bank_statements', 'bank_statement_lines', 'bank_payment_matches'))
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // 2. Missing app.tenant_id denies reads (0 rows) and writes (42501)
        foreach (var tbl in new[]
        {
            "ap.payment_sequences", "ap.supplier_payments", "ap.payment_allocations",
            "cash.bank_accounts", "cash.instruction_sequences", "cash.statement_sequences", "cash.match_sequences",
            "cash.payment_instructions", "cash.bank_statements", "cash.bank_statement_lines", "cash.bank_payment_matches"
        })
        {
            await using var cmd = appConn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM {tbl};";
            Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
        }

        await using (var noCtxWrite = appConn.CreateCommand())
        {
            noCtxWrite.CommandText = "INSERT INTO cash.match_sequences (tenant_id, legal_entity_id, fiscal_year, last_number) VALUES (@t, @e, 2026, 1);";
            noCtxWrite.Parameters.AddWithValue("t", world.TenantBId);
            noCtxWrite.Parameters.AddWithValue("e", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => noCtxWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // 3. Tenant B cannot read Tenant A cash/payment rows or write into Tenant A
        await using (var txB = await appConn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(appConn, txB, world.TenantBId);
            foreach (var tbl in new[] { "ap.supplier_payments", "cash.bank_accounts", "cash.payment_instructions", "cash.bank_statements", "cash.bank_payment_matches" })
            {
                await using var q = appConn.CreateCommand();
                q.Transaction = txB;
                q.CommandText = $"SELECT count(*) FROM {tbl} WHERE tenant_id = @ta;";
                q.Parameters.AddWithValue("ta", world.TenantAId);
                Assert.Equal(0L, Convert.ToInt64(await q.ExecuteScalarAsync()));
            }

            await AssertPgErrorInSavepointAsync(appConn, txB,
                $"INSERT INTO cash.match_sequences (tenant_id, legal_entity_id, fiscal_year, last_number) VALUES ('{world.TenantAId:D}', '{world.EntityA1Id:D}', 2027, 1);",
                "42501");
        }

        // 4. Runtime role (athera_app) append-only & immutability triggers
        await using (var txA = await appConn.BeginTransactionAsync())
        {
            await SetTenantContextAsync(appConn, txA, world.TenantAId);

            // Append-only GRANT denial (42501) on payment_instructions & bank_statements
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE cash.payment_instructions SET total_amount = 1 WHERE payment_instruction_id = '{instId:D}';", "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM cash.payment_instructions WHERE payment_instruction_id = '{instId:D}';", "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE cash.bank_statements SET closing_balance = 0 WHERE bank_statement_id = '{stmtId:D}';", "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM cash.bank_statements WHERE bank_statement_id = '{stmtId:D}';", "42501");

            // Released payment core financial fields immutable (55000); DELETE denied (42501)
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE ap.supplier_payments SET total_allocated_amount = 1 WHERE supplier_payment_id = '{payment.Payment.SupplierPaymentId:D}';", "55000");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM ap.supplier_payments WHERE supplier_payment_id = '{payment.Payment.SupplierPaymentId:D}';", "42501");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE ap.payment_allocations SET allocated_amount = 1 WHERE supplier_payment_id = '{payment.Payment.SupplierPaymentId:D}';", "55000");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"DELETE FROM ap.payment_allocations WHERE supplier_payment_id = '{payment.Payment.SupplierPaymentId:D}';", "55000");
            await AssertPgErrorInSavepointAsync(appConn, txA,
                $"UPDATE cash.bank_payment_matches SET matched_amount = 1 WHERE bank_payment_match_id = '{matchDto.BankPaymentMatchId:D}';", "55000");
        }
    }

    [Fact]
    public async Task S11_PG_02_MigrationM11DownToM10AndUpRoundTrip()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        await SeedWorldAsync(fx);

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261118135307_M10_SupplierReturnsReplacementsAndFinancialCorrections");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCmd = conn.CreateCommand();
            downCmd.CommandText = """
                SELECT count(*)
                FROM information_schema.tables
                WHERE (table_schema = 'ap' AND table_name IN ('payment_sequences', 'supplier_payments', 'payment_allocations'))
                   OR table_schema = 'cash';
                """;
            Assert.Equal(0L, Convert.ToInt64(await downCmd.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCmd = conn.CreateCommand();
            upCmd.CommandText = """
                SELECT count(*)
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE (
                    (n.nspname = 'ap' AND c.relname IN ('payment_sequences', 'supplier_payments', 'payment_allocations'))
                    OR n.nspname = 'cash'
                )
                  AND c.relrowsecurity
                  AND c.relforcerowsecurity;
                """;
            Assert.Equal(11L, Convert.ToInt64(await upCmd.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S11_API_01_SimulationNonPostingFourEyesReleaseBankReconcilePartialReversalBankRejectClosedPeriodAndSavepoint()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var apBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payables";
        var glBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";
        var payBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payments";

        var (bill1Id, pay1Id, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 10m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-API-01", "INV-API-01", "api01");
        var (billBlockedId, payBlockedId, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 5m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-API-02", "INV-API-02", "api02");

        // Block bill 2 payment and verify proposal fails closed
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync(
            $"{apBase}/bills/{billBlockedId:D}/payment-block",
            new UpdateBillPaymentBlockRequest(PaymentBlockStatuses.PaymentBlocked, "Hold inspection"))).StatusCode);

        var blockedProp = await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-1", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), null,
            [new PaymentAllocationInputRequest(payBlockedId, 555_000m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, blockedProp.StatusCode);
        Assert.Contains("payable_payment_blocked", await blockedProp.Content.ReadAsStringAsync());

        // Create valid proposal on pay1 (1,110,000), simulate payment file -> 0 GL, 0 AP PaidAmount!
        var glCountBeforeSim = await GetJournalCountAsync(controller, glBase);
        var propRes = await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-4455", "PT Sumber Makmur PKP",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), "Payment 1",
            [new PaymentAllocationInputRequest(pay1Id, 1_110_000m, "Full INV-API-01")]));
        Assert.Equal(HttpStatusCode.Created, propRes.StatusCode);
        var draft = (await propRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;

        var simRes = await apClerk.PostAsJsonAsync($"{payBase}/{draft.Payment.SupplierPaymentId:D}/simulate-file", new SimulatePaymentInstructionRequest(null));
        Assert.Equal(HttpStatusCode.OK, simRes.StatusCode);
        var simulated = (await simRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.True(simulated.Instructions[0].SimulationOnly);
        Assert.Equal(glCountBeforeSim, await GetJournalCountAsync(controller, glBase));

        var billAfterSim = (await (await apClerk.GetAsync($"{apBase}/bills/{bill1Id:D}")).Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        Assert.Equal(0m, billAfterSim.PayableItem!.PaidAmount);
        Assert.Equal(1_110_000m, billAfterSim.PayableItem!.RemainingOpenAmount);

        // Over-allocation draft exceeds available open balance -> 409 allocation_exceeds_open_payable
        var overAlloc = await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-4455", "PT Sumber Makmur PKP",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), null,
            [new PaymentAllocationInputRequest(pay1Id, 100_000m, null)]));
        Assert.Equal(HttpStatusCode.Conflict, overAlloc.StatusCode);
        Assert.Contains("allocation_exceeds_open_payable", await overAlloc.Content.ReadAsStringAsync());

        // Submit for release, verify closed fiscal period blocked (409 period_closed) BEFORE valid release
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{payBase}/{draft.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);

        var closedPeriodRelease = await controller.PostAsJsonAsync($"{payBase}/{draft.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 10, 15), "REL-CLOSED", "idem-closed"));
        Assert.Equal(HttpStatusCode.Conflict, closedPeriodRelease.StatusCode);
        Assert.Contains("period_closed", await closedPeriodRelease.Content.ReadAsStringAsync());

        // Four-eyes release posts Dr 2100 AP / Cr 1120 Clearing once, replay idempotent
        var relRes = await controller.PostAsJsonAsync($"{payBase}/{draft.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "BCA-PORTAL-9988", "idem-rel-1"));
        Assert.Equal(HttpStatusCode.OK, relRes.StatusCode);
        var released = (await relRes.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(SupplierPaymentStatuses.Released, released.Payment.Status);
        Assert.Equal(1_110_000m, released.Payment.RemainingClearingAmount);
        Assert.NotNull(released.Payment.ReleaseJournalNumber);
        Assert.Equal(glCountBeforeSim + 1, await GetJournalCountAsync(controller, glBase));

        var replayRel = await controller.PostAsJsonAsync($"{payBase}/{draft.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "BCA-PORTAL-9988", "idem-rel-1"));
        Assert.Equal(HttpStatusCode.OK, replayRel.StatusCode);
        var replayDto = (await replayRel.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.True(replayDto.IdempotentReplay);
        Assert.Equal(glCountBeforeSim + 1, await GetJournalCountAsync(controller, glBase));

        // Overview Clearing Proof shows Outbound Clearing 1120 == 1,110,000 Reconciled!
        var ov1 = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        Assert.Equal(1_110_000m, ov1.ClearingProof.GlOutboundClearingCreditNetBalance);
        Assert.Equal(1_110_000m, ov1.ClearingProof.SubledgerOpenClearingBalance);
        Assert.True(ov1.ClearingProof.IsReconciled);

        // Import statement with partial tranche 1 (500,000) & tranche 2 (610,000); duplicate statement & duplicate line blocked
        const string csv = """
            ValueDate,Direction,Amount,BankReference,Counterparty,Description
            2026-11-14,DEBIT,500000,TRF-PART-01,PT Sumber Makmur,Tranche 1
            2026-11-15,DEBIT,610000,TRF-PART-02,PT Sumber Makmur,Tranche 2
            """;
        var imp1 = await controller.PostAsJsonAsync($"{payBase}/bank-statements", new ImportBankStatementRequest(
            world.JktBankAccountId, "STMT-PARTIAL-01", new DateOnly(2026, 11, 15), 50_000_000m, 48_890_000m, csv, null));
        Assert.Equal(HttpStatusCode.Created, imp1.StatusCode);

        var dupRef = await controller.PostAsJsonAsync($"{payBase}/bank-statements", new ImportBankStatementRequest(
            world.JktBankAccountId, "STMT-PARTIAL-01", new DateOnly(2026, 11, 15), 50_000_000m, 48_890_000m, csv, null));
        Assert.Equal(HttpStatusCode.Conflict, dupRef.StatusCode);

        var dupLine = await controller.PostAsJsonAsync($"{payBase}/bank-statements", new ImportBankStatementRequest(
            world.JktBankAccountId, "STMT-OTHER-REF", new DateOnly(2026, 11, 15), 50_000_000m, 49_500_000m,
            "2026-11-14,DEBIT,500000,TRF-PART-01,PT Sumber Makmur,Duplicate line attempt", null));
        Assert.Equal(HttpStatusCode.Conflict, dupLine.StatusCode);
        Assert.Contains("duplicate_bank_statement_line", await dupLine.Content.ReadAsStringAsync());

        var ov2 = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        var part1Line = ov2.StatementLines.Single(l => l.BankReference == "TRF-PART-01");
        var part2Line = ov2.StatementLines.Single(l => l.BankReference == "TRF-PART-02");

        // Reconcile tranche 1 (500,000 -> PARTIALLY_RECONCILED, 610,000 remains in 1120)
        var rec1 = await controller.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(
            part1Line.BankStatementLineId, released.Payment.SupplierPaymentId, new DateOnly(2026, 11, 14), null, "idem-rec-1"));
        Assert.Equal(HttpStatusCode.OK, rec1.StatusCode);
        var match1 = (await rec1.Content.ReadFromJsonAsync<BankPaymentMatchDto>(JsonOptions))!;

        var ovAfterRec1 = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        Assert.Equal(610_000m, ovAfterRec1.ClearingProof.GlOutboundClearingCreditNetBalance);
        Assert.Equal(610_000m, ovAfterRec1.ClearingProof.SubledgerOpenClearingBalance);
        Assert.True(ovAfterRec1.ClearingProof.IsReconciled);

        // Reconcile tranche 2 (610,000 -> RECONCILED, Outbound Clearing 1120 == 0, Cash 1110 == -1,110,000)
        var rec2 = await controller.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(
            part2Line.BankStatementLineId, released.Payment.SupplierPaymentId, new DateOnly(2026, 11, 15), null, "idem-rec-2"));
        Assert.Equal(HttpStatusCode.OK, rec2.StatusCode);
        var match2 = (await rec2.Content.ReadFromJsonAsync<BankPaymentMatchDto>(JsonOptions))!;

        var ovFull = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        Assert.Equal(0m, ovFull.ClearingProof.GlOutboundClearingCreditNetBalance);
        Assert.Equal(0m, ovFull.ClearingProof.SubledgerOpenClearingBalance);
        Assert.True(ovFull.ClearingProof.IsReconciled);
        Assert.Equal(-1_110_000m, ovFull.ClearingProof.GlMainOperatingBankNetBalance);

        // Cannot settle same statement line twice
        var dupSettle = await controller.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(
            part2Line.BankStatementLineId, released.Payment.SupplierPaymentId, new DateOnly(2026, 11, 15), 10_000m, "idem-rec-dup"));
        Assert.Equal(HttpStatusCode.Conflict, dupSettle.StatusCode);

        // Reverse both bank matches -> cannot reject payment until ALL matches are reversed
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{payBase}/matches/{match1.BankPaymentMatchId:D}/reverse",
            new ReverseBankMatchRequest(new DateOnly(2026, 11, 15), "Correction 1"))).StatusCode);

        // Reject while still partially reconciled fails closed (409 payment_already_reconciled_reverse_match_first)
        var badReject = await controller.PostAsJsonAsync($"{payBase}/{released.Payment.SupplierPaymentId:D}/reject-released",
            new RejectReleasedPaymentRequest(new DateOnly(2026, 11, 15), "Still partially matched"));
        Assert.Equal(HttpStatusCode.Conflict, badReject.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{payBase}/matches/{match2.BankPaymentMatchId:D}/reverse",
            new ReverseBankMatchRequest(new DateOnly(2026, 11, 15), "Correction 2"))).StatusCode);

        // Now reject released payment -> restores PayableItem back to OPEN (Paid=0, Remaining=1,110,000) & Outbound Clearing == 0!
        var rejOk = await controller.PostAsJsonAsync($"{payBase}/{released.Payment.SupplierPaymentId:D}/reject-released",
            new RejectReleasedPaymentRequest(new DateOnly(2026, 11, 15), "Bank rejected invalid beneficiary account"));
        Assert.Equal(HttpStatusCode.OK, rejOk.StatusCode);
        var rejectedDto = (await rejOk.Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(SupplierPaymentStatuses.BankRejected, rejectedDto.Payment.Status);

        var ovRestored = (await (await controller.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        Assert.Equal(0m, ovRestored.ClearingProof.GlOutboundClearingCreditNetBalance);
        Assert.True(ovRestored.ClearingProof.IsReconciled);
        var restoredPayable = ovRestored.OpenPayables.Single(p => p.PayableItemId == pay1Id);
        Assert.Equal(PayableStatuses.Open, restoredPayable.Status);
        Assert.Equal(0m, restoredPayable.PaidAmount);
        Assert.Equal(1_110_000m, restoredPayable.RemainingOpenAmount);

        // Savepoint rollback proof on injected fault inside ReleasePaymentAsync
        var (_, payFaultId, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 4m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 11), "SJ-FAULT-01", "INV-FAULT-01", "fault01");
        var faultProp = (await (await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-4455", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 14), null,
            [new PaymentAllocationInputRequest(payFaultId, 444_000m, null)]))).Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{payBase}/{faultProp.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);

        var appOptions = new DbContextOptionsBuilder<AtheraDbContext>()
            .UseAtheraNpgsql(fx.RuntimeConnectionString)
            .Options;
        await using var appDb = new AtheraDbContext(appOptions);
        await using (var tx = await appDb.Database.BeginTransactionAsync())
        {
            await appDb.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT set_config('app.tenant_id', {world.TenantAId.ToString("D")}, true);");
            await Assert.ThrowsAsync<ApplicationException>(() =>
                SupplierSettlementCoordinator.ReleasePaymentAsync(
                    appDb,
                    new ReleasePaymentCommand(
                        world.TenantAId, world.EntityA1Id, faultProp.Payment.SupplierPaymentId, "IDR",
                        new DateOnly(2026, 11, 14), "FAULT-REF", "idem-fault-rel", world.ControllerId, DateTimeOffset.UtcNow,
                        OnAfterGlPostedBeforeCommitAsync: () => throw new ApplicationException("simulated_fault")),
                    CancellationToken.None));
            await tx.CommitAsync();
        }

        // Verify payment remained PENDING_RELEASE with zero release journal
        var afterFault = (await (await controller.GetAsync($"{payBase}/{faultProp.Payment.SupplierPaymentId:D}")).Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(SupplierPaymentStatuses.PendingRelease, afterFault.Payment.Status);
        Assert.Null(afterFault.Payment.ReleaseJournalId);
    }

    [Fact]
    public async Task S11_API_02_ConcurrencyRacesAndIdempotentReplays()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var ap1 = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var ap2 = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var ctrl1 = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);
        using var ctrl2 = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);

        var payBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payments";
        var (_, payId, _) = await AcceptAndPostBillAsync(
            buyer, ap1, ctrl1, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 10m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-RACE-01", "INV-RACE-01", "race01");

        // Race 1: Two concurrent full payment proposals on the same open payable -> 1 Created, 1 Conflict
        var propReq = new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "PT Bank Mandiri", "122-000-4455", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), "Concurrent proposal",
            [new PaymentAllocationInputRequest(payId, 1_110_000m, null)]);
        var propTasks = await Task.WhenAll(
            ap1.PostAsJsonAsync(payBase, propReq),
            ap2.PostAsJsonAsync(payBase, propReq));
        Assert.Single(propTasks, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(propTasks, r => r.StatusCode == HttpStatusCode.Conflict);

        var wonProp = (await propTasks.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await ap1.PostAsJsonAsync($"{payBase}/{wonProp.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);

        // Race 2: Two concurrent releases with different keys -> 1 OK, 1 Conflict (409)
        var relTasks = await Task.WhenAll(
            ctrl1.PostAsJsonAsync($"{payBase}/{wonProp.Payment.SupplierPaymentId:D}/release",
                new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "REL-R1", "race-rel-1")),
            ctrl2.PostAsJsonAsync($"{payBase}/{wonProp.Payment.SupplierPaymentId:D}/release",
                new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "REL-R2", "race-rel-2")));
        Assert.Single(relTasks, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(relTasks, r => r.StatusCode == HttpStatusCode.Conflict);

        // Import statement line for concurrency reconcile race
        Assert.Equal(HttpStatusCode.Created, (await ctrl1.PostAsJsonAsync($"{payBase}/bank-statements", new ImportBankStatementRequest(
            world.JktBankAccountId, "STMT-RACE-01", new DateOnly(2026, 11, 15), 20_000_000m, 18_890_000m,
            "2026-11-15,DEBIT,1110000,TRF-RACE-01,PT Sumber Makmur,Race debit", null))).StatusCode);
        var ov = (await (await ctrl1.GetAsync($"{payBase}/overview")).Content.ReadFromJsonAsync<PaymentsOverviewResponse>(JsonOptions))!;
        var lineId = ov.StatementLines.Single(l => l.BankReference == "TRF-RACE-01").BankStatementLineId;

        // Race 3: Two concurrent reconciliations with different idempotency keys -> 1 OK, 1 Conflict
        var recTasks = await Task.WhenAll(
            ctrl1.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(lineId, wonProp.Payment.SupplierPaymentId, new DateOnly(2026, 11, 15), 1_110_000m, "rec-race-1")),
            ctrl2.PostAsJsonAsync($"{payBase}/reconcile", new ReconcileBankStatementLineRequest(lineId, wonProp.Payment.SupplierPaymentId, new DateOnly(2026, 11, 15), 1_110_000m, "rec-race-2")));
        Assert.Single(recTasks, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(recTasks, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task S11_API_03_FourEyesSodRbacReadOnlyEntityCrossTenant404AndAuditOutbox()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        using var factory = fx.CreateApiFactory();
        using var buyer = await CreateSignedInClientAsync(factory, world.BuyerEmail, world.Password);
        using var apClerk = await CreateSignedInClientAsync(factory, world.ApClerkEmail, world.Password);
        using var controller = await CreateSignedInClientAsync(factory, world.ControllerEmail, world.Password);
        using var foreign = await CreateSignedInClientAsync(factory, world.ForeignEmail, world.Password);

        var payBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/payments";
        var sbyPayBase = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/payments";

        var (_, payId, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 6m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-RBAC-01", "INV-RBAC-01", "rbac01");

        // Buyer lacks ap.*/finance.* -> 403 on overview and create proposal
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync($"{payBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "Bank", "123", "Holder", PaymentMethods.BankTransferIdr,
            new DateOnly(2026, 11, 12), null, [new PaymentAllocationInputRequest(payId, 666_000m, null)]))).StatusCode);

        // AP Clerk prepares & submits, lacks release/reconcile perms -> 403
        var prop = (await (await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "Bank Mandiri", "122-999", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), null,
            [new PaymentAllocationInputRequest(payId, 666_000m, null)]))).Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{payBase}/{prop.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await apClerk.PostAsJsonAsync(
            $"{payBase}/{prop.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "SELF-ATTEMPT", "k-self"))).StatusCode);

        // OD-02 SoD: Controller cannot release their OWN prepared payment (create as controller -> self-release -> 403 four_eyes_violation)
        var (_, pay2Id, _) = await AcceptAndPostBillAsync(
            buyer, apClerk, controller, world.TenantAId, world.EntityA1Id,
            world.SupplierPkpId, world.ItemId, world.PcsUomId, 3m, 100_000m, world.PpnInputId,
            new DateOnly(2026, 11, 10), "SJ-RBAC-02", "INV-RBAC-02", "rbac02");
        var ctrlProp = (await (await controller.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.JktBankAccountId, world.SupplierPkpId, "Bank Mandiri", "122-999", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), null,
            [new PaymentAllocationInputRequest(pay2Id, 333_000m, null)]))).Content.ReadFromJsonAsync<SupplierPaymentDetailResponse>(JsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{payBase}/{ctrlProp.Payment.SupplierPaymentId:D}/submit", new { })).StatusCode);
        var selfRel = await controller.PostAsJsonAsync($"{payBase}/{ctrlProp.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "CTRL-SELF", "k-ctrl-self"));
        Assert.Equal(HttpStatusCode.Forbidden, selfRel.StatusCode);
        Assert.Contains("four_eyes_violation", await selfRel.Content.ReadAsStringAsync());

        // Foreign bank account in NUSA-SBY rejected against NUSA-JKT payable (400/404)
        var crossEntityBank = await apClerk.PostAsJsonAsync(payBase, new CreateSupplierPaymentProposalRequest(
            world.SbyBankAccountId, world.SupplierPkpId, "Bank Mandiri", "122-999", "PT Sumber Makmur",
            PaymentMethods.BankTransferIdr, new DateOnly(2026, 11, 12), null,
            [new PaymentAllocationInputRequest(pay2Id, 333_000m, null)]));
        Assert.Equal(HttpStatusCode.BadRequest, crossEntityBank.StatusCode);

        // Read-only Entity A2 (NUSA-SBY): Controller GET 200, POST 403
        Assert.Equal(HttpStatusCode.OK, (await controller.GetAsync($"{sbyPayBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await controller.PostAsJsonAsync(sbyPayBase, new CreateSupplierPaymentProposalRequest(
            world.SbyBankAccountId, world.SupplierPkpId, "Bank", "123", "Holder", PaymentMethods.BankTransferIdr,
            new DateOnly(2026, 11, 12), null, [new PaymentAllocationInputRequest(payId, 100m, null)]))).StatusCode);

        // Tenant B hostile selector -> 404
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"{payBase}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"{payBase}/{prop.Payment.SupplierPaymentId:D}")).StatusCode);

        // Release Siti's proposal as Budi & reconcile -> verify AuditEvent & OutboxMessage
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{payBase}/{prop.Payment.SupplierPaymentId:D}/release",
            new ReleaseSupplierPaymentRequest(new DateOnly(2026, 11, 12), "REL-RBAC", "k-rel-rbac"))).StatusCode);

        await using var db = fx.CreateMigratorDbContext();
        var events = await db.AuditEvents.AsNoTracking()
            .Where(a => a.TenantId == world.TenantAId && a.SubjectId == prop.Payment.SupplierPaymentId.ToString("D"))
            .Select(a => a.EventType)
            .ToListAsync();
        Assert.Contains("AP_PAYMENT_PROPOSAL_CREATED", events);
        Assert.Contains("AP_PAYMENT_SUBMITTED", events);
        Assert.Contains("CASH_PAYMENT_RELEASED_TO_CLEARING", events);

        var outboxTopics = await db.OutboxMessages.AsNoTracking()
            .Where(o => o.TenantId == world.TenantAId && o.LegalEntityId == world.EntityA1Id)
            .Select(o => o.Topic)
            .ToListAsync();
        Assert.Contains("cash.supplier_payment.submitted.v1", outboxTopics);
        Assert.Contains("cash.supplier_payment.released.v1", outboxTopics);
    }

    private static async Task<int> GetJournalCountAsync(HttpClient client, string glBase)
    {
        var paged = (await (await client.GetAsync($"{glBase}/journals")).Content.ReadFromJsonAsync<PagedJournalsResponse>(JsonOptions))!;
        return paged.TotalCount;
    }

    private static async Task<(Guid BillId, Guid PayableItemId, string PayableNumber)> AcceptAndPostBillAsync(
        HttpClient buyer,
        HttpClient apClerk,
        HttpClient controller,
        Guid tenantId,
        Guid entityId,
        Guid supplierId,
        Guid itemId,
        Guid uomId,
        decimal qty,
        decimal unitPrice,
        Guid ppnTaxId,
        DateOnly date,
        string deliveryNote,
        string invoiceNo,
        string idempBase)
    {
        var procBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/procurement";
        var rcvBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/receiving";
        var apBase = $"/api/workspaces/{tenantId:D}/entities/{entityId:D}/payables";

        var createPo = await buyer.PostAsJsonAsync($"{procBase}/purchase-orders", new CreatePurchaseOrderRequest(
            supplierId, "IDR", date, date.AddDays(14), "Gudang Jakarta", 30, null, null,
            [new PoLineInputDto("L001", itemId, uomId, qty, unitPrice, null)]));
        Assert.Equal(HttpStatusCode.Created, createPo.StatusCode);
        var po = (await createPo.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await buyer.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{po.Revisions[0].RevisionId:D}/submit",
            new SubmitPoRevisionRequest(null))).StatusCode);
        var appPo = await controller.PostAsJsonAsync(
            $"{procBase}/purchase-orders/{po.PurchaseOrderId:D}/revisions/{po.Revisions[0].RevisionId:D}/decide",
            new DecidePoRevisionRequest(ApprovalDecisions.Approved, "Approved"));
        Assert.Equal(HttpStatusCode.OK, appPo.StatusCode);
        var approvedPo = (await appPo.Content.ReadFromJsonAsync<PurchaseOrderDetailDto>(JsonOptions))!;
        var poLine = approvedPo.Revisions[0].Lines[0];

        var arrRes = await buyer.PostAsJsonAsync($"{rcvBase}/receipts", new RecordGoodsReceiptRequest(
            approvedPo.PurchaseOrderId, deliveryNote, $"{idempBase}-gr", date, "Dermaga A", null, null, null,
            [new ArrivalLineRequest(poLine.PoLineId, uomId, qty, false, DiscrepancyReasonCodes.None, null, null)]));
        Assert.Equal(HttpStatusCode.Created, arrRes.StatusCode);
        var grn = (await arrRes.Content.ReadFromJsonAsync<GoodsReceiptDetailDto>(JsonOptions))!;

        var dcRes = await buyer.PostAsJsonAsync(
            $"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/lines/{grn.Lines[0].GoodsReceiptLineId:D}/dock-checks",
            new RecordDockCheckRequest($"{idempBase}-dc", 0m, 0m, qty, 0m, DiscrepancyReasonCodes.None, "OK", null));
        Assert.Equal(HttpStatusCode.OK, dcRes.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await buyer.PostAsJsonAsync($"{rcvBase}/receipts/{grn.GoodsReceiptId:D}/accept",
            new AcceptReceiptRequest($"{idempBase}-acc", date, StockStatuses.Unrestricted, "Accept", [new AcceptReceiptLineRequest(grn.Lines[0].GoodsReceiptLineId, qty)]))).StatusCode);

        var apOv = (await (await controller.GetAsync($"{apBase}/overview")).Content.ReadFromJsonAsync<PayablesOverviewResponse>(JsonOptions))!;
        var accLine = apOv.EligibleAccrualLines.Single(l => l.PoLineId == poLine.PoLineId);

        var sub = qty * unitPrice;
        var ppn = decimal.Round(sub * 0.11m, 0, MidpointRounding.AwayFromZero);
        var declaredGross = sub + ppn;

        var billRes = await apClerk.PostAsJsonAsync($"{apBase}/bills", new CreateSupplierBillRequest(
            SupplierBillTypes.PoThreeWayMatch, approvedPo.PurchaseOrderId, invoiceNo, "010.001-26.90001", null,
            date, date.AddDays(30), "IDR", declaredGross, "S11 fixture",
            [new BillLineInputRequest(accLine.ReceiptAccrualLineId, qty, unitPrice, ppnTaxId, null, null)]));
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);
        var draft = (await billRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;

        Assert.Equal(HttpStatusCode.OK, (await apClerk.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/submit", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await controller.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/decision", new DecideSupplierBillRequest(ApprovalDecisions.Approved, "Approved", false, false, null))).StatusCode);
        var postRes = await controller.PostAsJsonAsync($"{apBase}/bills/{draft.Bill.SupplierBillId:D}/post", new PostSupplierBillRequest(date, $"{idempBase}-post"));
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var posted = (await postRes.Content.ReadFromJsonAsync<SupplierBillDetailResponse>(JsonOptions))!;
        return (posted.Bill.SupplierBillId, posted.PayableItem!.PayableItemId, posted.PayableItem!.PayableNumber);
    }

    private static async Task AssertPgErrorInSavepointAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, string expectedSqlState)
    {
        await using var sp = conn.CreateCommand();
        sp.Transaction = tx;
        sp.CommandText = "SAVEPOINT s_err;";
        await sp.ExecuteNonQueryAsync();

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal(expectedSqlState, ex.SqlState);

        await using var rb = conn.CreateCommand();
        rb.Transaction = tx;
        rb.CommandText = "ROLLBACK TO SAVEPOINT s_err;";
        await rb.ExecuteNonQueryAsync();
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tenantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
        cmd.Parameters.AddWithValue("tid", tenantId.ToString("D"));
        await cmd.ExecuteScalarAsync();
    }

    private static async Task<HttpClient> CreateSignedInClientAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
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

    private static async Task<S11World> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (buyerId, buyerEmail, _) = await fx.CreateTestUserAsync("rina.s11@test.local", "Rina S11 Buyer", pw);
        var (apClerkId, apClerkEmail, _) = await fx.CreateTestUserAsync("siti.s11@test.local", "Siti S11 AP Clerk", pw);
        var (controllerId, controllerEmail, _) = await fx.CreateTestUserAsync("budi.s11@test.local", "Budi S11 Controller", pw);
        var (foreignId, foreignEmail, _) = await fx.CreateTestUserAsync("foreign.s11@test.local", "Samudra Staff", pw);

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
            TenantMembership.Create(Guid.NewGuid(), tenantAId, buyerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, apClerkId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, controllerId, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, foreignId, now));

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementManage,
            PermissionCodes.ReceivingRead, PermissionCodes.ReceivingManage, PermissionCodes.ReceivingAccept,
            PermissionCodes.InventoryRead, PermissionCodes.InventoryManage, PermissionCodes.ApprovalRequest
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, buyerId, perm, now, null, now));
        }

        foreach (var perm in RoleTemplates.GetPermissions(RoleTemplates.ApClerk))
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, apClerkId, perm, now, null, now));
        }

        foreach (var perm in new[]
        {
            PermissionCodes.WorkspaceView, PermissionCodes.CatalogRead, PermissionCodes.PartnersRead,
            PermissionCodes.ProcurementRead, PermissionCodes.ProcurementApprove,
            PermissionCodes.ReceivingRead, PermissionCodes.InventoryRead,
            PermissionCodes.FinanceRead, PermissionCodes.FinanceConfigManage, PermissionCodes.FinanceJournalPost,
            PermissionCodes.ApRead, PermissionCodes.ApManage, PermissionCodes.ApApprove, PermissionCodes.ApPost,
            PermissionCodes.ApprovalRequest, PermissionCodes.ApprovalDecide
        })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, controllerId, perm, now, null, now));
        }

        // Read-only Entity A2 (NUSA-SBY) for Controller
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.WorkspaceView, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.FinanceRead, now, null, now));
        db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, controllerId, PermissionCodes.ApRead, now, null, now));

        foreach (var perm in new[] { PermissionCodes.WorkspaceView, PermissionCodes.ProcurementRead, PermissionCodes.ApRead })
        {
            db.EntityAccessGrants.Add(EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, foreignId, perm, now, null, now));
        }

        db.ApprovalPolicies.AddRange(
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, PurchaseOrderWorkflowService.PoApprovalPolicyCode, "PO Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.SupplierBillApprovalPolicyCode, "Bill Approval", true, false, now),
            ApprovalPolicy.Create(Guid.NewGuid(), tenantAId, entityA1Id, CashPolicyDefaults.SupplierPaymentReleasePolicyCode, "Payment Release", true, false, now));
        db.DeliveryTolerancePolicies.Add(DeliveryTolerancePolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, "EXACT_SAFE", "Exact Qty OD-01", 0m, 0m, false, true, now));
        db.InvoiceMatchPolicies.Add(InvoiceMatchPolicy.CreateSafeDefault(Guid.NewGuid(), tenantAId, entityA1Id, ApPolicyDefaults.DefaultMatchPolicyCode, "Exact OD-03 Safe", true, now));

        var bankOpAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1110", "Kas & Bank Operasional", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var clearAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1120", "Kliring Pembayaran Keluar", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, null, true, true, now);
        var invAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1300", "Persediaan Barang Dagang", GlAccountTypes.Asset, GlAccountCategories.InventoryAsset, null, true, true, now);
        var ppnAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "1410", "PPN Masukan", GlAccountTypes.Asset, GlAccountCategories.TaxReceivable, null, true, true, now);
        var apAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2100", "Hutang Usaha", GlAccountTypes.Liability, GlAccountCategories.AccountsPayable, null, true, true, now);
        var grniAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, null, true, true, now);
        var cogsAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5100", "HPP", GlAccountTypes.Expense, GlAccountCategories.CogsExpense, null, true, true, now);
        var ppvAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA1Id, "5200", "PPV", GlAccountTypes.Expense, GlAccountCategories.PurchasePriceVariance, null, true, true, now);

        var sbyBankOpAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA2Id, "1110", "Kas & Bank Surabaya", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now);
        var sbyClearAcc = GlAccount.Create(Guid.NewGuid(), tenantAId, entityA2Id, "1120", "Kliring Keluar Surabaya", GlAccountTypes.Asset, GlAccountCategories.BankOutboundClearing, null, true, true, now);
        db.GlAccounts.AddRange(bankOpAcc, clearAcc, invAcc, ppnAcc, apAcc, grniAcc, cogsAcc, ppvAcc, sbyBankOpAcc, sbyClearAcc);

        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-10", 2026, 10, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), FinancialPeriodStatuses.Closed, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        db.AccountMappings.AddRange(
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankMainOperating, bankOpAcc, "Bank", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.BankOutboundClearing, clearAcc, "Clear", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.InventoryAsset, invAcc, "Inv", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.GrniClearing, grniAcc, "GRNI", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.ApTradeControl, apAcc, "AP", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PurchasePriceVariance, ppvAcc, "PPV", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.CogsDefault, cogsAcc, "COGS", controllerId, now),
            AccountMapping.Create(Guid.NewGuid(), tenantAId, entityA1Id, GlMappingPurposes.PpnInputTax, ppnAcc, "PPN-In", controllerId, now));

        var jktBank = BankAccount.Create(
            Guid.NewGuid(), tenantAId, entityA1Id, "BCA-IDR-001", "PT Bank Central Asia Tbk",
            "Sudirman Jakarta", "035-9988-771", "PT Nusantara Distribusi Jakarta", "IDR",
            bankOpAcc.AccountId, clearAcc.AccountId, controllerId, now);
        var sbyBank = BankAccount.Create(
            Guid.NewGuid(), tenantAId, entityA2Id, "BCA-SBY-001", "PT Bank Central Asia Tbk",
            "Pemuda Surabaya", "088-4411-220", "PT Nusantara Niaga Surabaya", "IDR",
            sbyBankOpAcc.AccountId, sbyClearAcc.AccountId, controllerId, now);
        db.BankAccounts.AddRange(jktBank, sbyBank);

        var ppnCode = TaxCode.Create(Guid.NewGuid(), tenantAId, entityA1Id, "PPN-IN-11", "PPN Masukan 11%", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.StandardTaxable, 11m, 1, 1, false, false, ppnAcc.AccountId, new DateOnly(2026, 1, 1), null, TaxReviewStatuses.UnreviewedConfigurable, null, now);
        db.TaxCodes.Add(ppnCode);

        var supPkp = Partner.Create(Guid.NewGuid(), tenantAId, "SUP-A", "PT Sumber Makmur PKP", "Sumber Makmur", true, false, "01.123.456.7-011.000", TaxPkpStatuses.Pkp, EntityScopeModes.AllEntities, now);
        db.Partners.Add(supPkp);

        var pcs = UnitOfMeasure.Create(Guid.NewGuid(), tenantAId, "PCS", "Pieces", 0, now);
        db.UnitsOfMeasure.Add(pcs);

        var item = Item.Create(Guid.NewGuid(), tenantAId, "ITM-S11-FILTER-01", "Filter Oli Hidrolik", ItemKinds.StockedGood, pcs.UomId, pcs.UomId, pcs.UomId, EntityScopeModes.AllEntities, now);
        db.Items.Add(item);

        await db.SaveChangesAsync();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await InventoryAvailabilityService.EnsureDefaultWarehouseAndLocationsAsync(db, tenantAId, entityA1Id, now, CancellationToken.None);
            await tx.CommitAsync();
        }

        return new S11World(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            buyerId, buyerEmail, apClerkId, apClerkEmail, controllerId, controllerEmail, foreignId, foreignEmail,
            supPkp.PartnerId, pcs.UomId, item.ItemId, ppnCode.TaxCodeId, jktBank.BankAccountId, sbyBank.BankAccountId, pw);
    }
}
