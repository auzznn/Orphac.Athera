using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.GeneralLedger;
using Orphac.Athera.Modules.Identity;
using Orphac.Athera.Modules.Platform;
using Orphac.Athera.Modules.Tax;

namespace Orphac.Athera.IntegrationTests;

public sealed class S04FinancialKernelPostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record S04SeedWorld(
        Guid TenantAId,
        Guid EntityA1Id,
        Guid EntityA2Id,
        Guid TenantBId,
        Guid EntityB1Id,
        Guid FinanceA1Id,
        string FinanceA1Email,
        Guid ReadA2Id,
        string ReadA2Email,
        Guid NonFinanceA1Id,
        string NonFinanceA1Email,
        Guid FinanceB1Id,
        string FinanceB1Email,
        Guid CashA1Id,
        Guid EquityA1Id,
        Guid ForeignCashB1Id,
        Guid PeriodA1Id,
        string Password);

    [Fact]
    public async Task S04_PG_01_MissingContextDeniesAndCrossTenantFinanceIsolatedUnderForcedRls()
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
                    (n.nspname = 'gl' AND c.relname IN ('accounts', 'financial_periods', 'account_mappings', 'journal_sequences', 'journal_entries', 'journal_lines'))
                    OR (n.nspname = 'tax' AND c.relname IN ('tax_codes'))
                )
                AND c.relrowsecurity
                AND c.relforcerowsecurity;
                """;
            Assert.Equal(7L, Convert.ToInt64(await rlsCmd.ExecuteScalarAsync()));
        }

        // Without app.tenant_id, runtime role sees zero GL/Tax rows and cannot insert
        await using (var zeroRead = conn.CreateCommand())
        {
            zeroRead.CommandText = "SELECT count(*) FROM gl.accounts";
            Assert.Equal(0L, Convert.ToInt64(await zeroRead.ExecuteScalarAsync()));
        }

        await using (var writeNoCtx = conn.CreateCommand())
        {
            writeNoCtx.CommandText = """
                INSERT INTO gl.accounts (account_id, tenant_id, legal_entity_id, account_code, name, account_type, account_category, normal_balance, allow_posting, is_control_account, status, created_at_utc, updated_at_utc, version)
                VALUES (@aid, @tid, @eid, '9999', 'Rogue Account', 'ASSET', 'GENERAL', 'DEBIT', true, false, 'ACTIVE', now(), now(), 1);
                """;
            writeNoCtx.Parameters.AddWithValue("aid", Guid.NewGuid());
            writeNoCtx.Parameters.AddWithValue("tid", world.TenantBId);
            writeNoCtx.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => writeNoCtx.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        // Scoped to Tenant A, Tenant B accounts are invisible and cross-tenant write is denied
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        await using (var readForeign = conn.CreateCommand())
        {
            readForeign.Transaction = tx;
            readForeign.CommandText = "SELECT count(*) FROM gl.accounts WHERE account_id = @bid;";
            readForeign.Parameters.AddWithValue("bid", world.ForeignCashB1Id);
            Assert.Equal(0L, Convert.ToInt64(await readForeign.ExecuteScalarAsync()));
        }

        await tx.SaveAsync("sp_cross_insert");
        await using (var crossWrite = conn.CreateCommand())
        {
            crossWrite.Transaction = tx;
            crossWrite.CommandText = """
                INSERT INTO gl.accounts (account_id, tenant_id, legal_entity_id, account_code, name, account_type, account_category, normal_balance, allow_posting, is_control_account, status, created_at_utc, updated_at_utc, version)
                VALUES (@aid, @tid, @eid, '9998', 'Cross Account', 'ASSET', 'GENERAL', 'DEBIT', true, false, 'ACTIVE', now(), now(), 1);
                """;
            crossWrite.Parameters.AddWithValue("aid", Guid.NewGuid());
            crossWrite.Parameters.AddWithValue("tid", world.TenantBId);
            crossWrite.Parameters.AddWithValue("eid", world.EntityB1Id);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => crossWrite.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }

        await tx.RollbackAsync("sp_cross_insert");
        await tx.CommitAsync();
    }

    [Fact]
    public async Task S04_PG_02_MigrationM04UpDownUpRoundTripSucceeds()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();

        await using var db = fx.CreateMigratorDbContext();
        await db.Database.MigrateAsync("20261117191830_M03_PartnersCatalogueAndUom");

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var downCheck = conn.CreateCommand();
            downCheck.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema IN ('gl', 'tax');";
            Assert.Equal(0L, Convert.ToInt64(await downCheck.ExecuteScalarAsync()));
        }

        await db.Database.MigrateAsync();

        await using (var conn = new NpgsqlConnection(fx.MigratorConnectionString))
        {
            await conn.OpenAsync();
            await using var upCheck = conn.CreateCommand();
            upCheck.CommandText = "SELECT count(*) FROM information_schema.tables WHERE (table_schema = 'gl' AND table_name IN ('accounts', 'financial_periods', 'account_mappings', 'journal_entries', 'journal_sequences', 'journal_lines')) OR (table_schema = 'tax' AND table_name = 'tax_codes');";
            Assert.Equal(7L, Convert.ToInt64(await upCheck.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task S04_PG_03_JournalTablesAppendOnlyAndDeferredSqlTriggerBlocksUnbalancedRawSql()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);

        var journalId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var migDb = fx.CreateMigratorDbContext())
        {
            await using var seedTx = await migDb.Database.BeginTransactionAsync();
            var (entry, lines) = JournalEntry.CreatePosted(
                journalId,
                world.TenantAId,
                world.EntityA1Id,
                world.PeriodA1Id,
                "JRN-2026-11-90001",
                JournalEntryKinds.Standard,
                new DateOnly(2026, 11, 18),
                "IDR",
                "Immutable seed",
                "MANUAL_GL",
                "MANUAL_JOURNAL",
                "raw-01",
                "raw:seed-01",
                null,
                [
                    new(world.CashA1Id, "Dr Cash", 500_000m, 0m),
                    new(world.EquityA1Id, "Cr Capital", 0m, 500_000m)
                ],
                world.FinanceA1Id,
                now);
            migDb.JournalEntries.Add(entry);
            migDb.JournalLines.AddRange(lines);
            await migDb.SaveChangesAsync();
            await seedTx.CommitAsync();
        }

        await using var conn = new NpgsqlConnection(fx.RuntimeConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await SetTenantContextAsync(conn, tx, world.TenantAId);

        // Runtime role athera_app cannot UPDATE or DELETE journal_entries or journal_lines (42501)
        await tx.SaveAsync("sp_upd_entry");
        await using (var updEntry = conn.CreateCommand())
        {
            updEntry.Transaction = tx;
            updEntry.CommandText = "UPDATE gl.journal_entries SET description = 'tamper' WHERE journal_id = @jid;";
            updEntry.Parameters.AddWithValue("jid", journalId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updEntry.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_upd_entry");

        await tx.SaveAsync("sp_del_entry");
        await using (var delEntry = conn.CreateCommand())
        {
            delEntry.Transaction = tx;
            delEntry.CommandText = "DELETE FROM gl.journal_entries WHERE journal_id = @jid;";
            delEntry.Parameters.AddWithValue("jid", journalId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delEntry.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_del_entry");

        await tx.SaveAsync("sp_upd_line");
        await using (var updLine = conn.CreateCommand())
        {
            updLine.Transaction = tx;
            updLine.CommandText = "UPDATE gl.journal_lines SET debit_amount = 999 WHERE journal_id = @jid;";
            updLine.Parameters.AddWithValue("jid", journalId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => updLine.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_upd_line");

        await tx.SaveAsync("sp_del_line");
        await using (var delLine = conn.CreateCommand())
        {
            delLine.Transaction = tx;
            delLine.CommandText = "DELETE FROM gl.journal_lines WHERE journal_id = @jid;";
            delLine.Parameters.AddWithValue("jid", journalId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => delLine.ExecuteNonQueryAsync());
            Assert.Equal("42501", ex.SqlState);
        }
        await tx.RollbackAsync("sp_del_line");

        // Deferred trigger catches mismatched header/lines on COMMIT even if header says balanced!
        var badJournalId = Guid.NewGuid();
        await using (var insertBadHeader = conn.CreateCommand())
        {
            insertBadHeader.Transaction = tx;
            insertBadHeader.CommandText = """
                INSERT INTO gl.journal_entries (
                    journal_id, tenant_id, legal_entity_id, period_id, journal_number, entry_kind,
                    posting_date, currency_code, description, source_module, source_document_type,
                    source_document_id, source_event_key, payload_hash, total_debit, total_credit,
                    line_count, posted_by_user_id, posted_at_utc)
                VALUES (
                    @jid, @tid, @eid, @pid, 'JRN-ROGUE-1', 'STANDARD',
                    '2026-11-18', 'IDR', 'Bypass attempt', 'RAW', 'RAW',
                    '1', 'raw:bypass', 'hash', 1000, 1000, 2, @uid, now());
                INSERT INTO gl.journal_lines (
                    line_id, tenant_id, legal_entity_id, journal_id, line_number, account_id, debit_amount, credit_amount)
                VALUES
                    (gen_random_uuid(), @tid, @eid, @jid, 1, @cash, 1000, 0),
                    (gen_random_uuid(), @tid, @eid, @jid, 2, @eq, 0, 500);
                """;
            insertBadHeader.Parameters.AddWithValue("jid", badJournalId);
            insertBadHeader.Parameters.AddWithValue("tid", world.TenantAId);
            insertBadHeader.Parameters.AddWithValue("eid", world.EntityA1Id);
            insertBadHeader.Parameters.AddWithValue("pid", world.PeriodA1Id);
            insertBadHeader.Parameters.AddWithValue("uid", world.FinanceA1Id);
            insertBadHeader.Parameters.AddWithValue("cash", world.CashA1Id);
            insertBadHeader.Parameters.AddWithValue("eq", world.EquityA1Id);
            await insertBadHeader.ExecuteNonQueryAsync();
        }

        var commitEx = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
        Assert.Contains("gl_journal_unbalanced", commitEx.MessageText);
    }

    [Fact]
    public async Task S04_API_01_CoaPeriodsMappingsAndBalancedJournalPostAtomically()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var finA1 = await CreateSignedInClientAsync(factory, world.FinanceA1Email, world.Password);
        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";

        // 1. Header account cannot accept postings
        var hdrRes = await finA1.PostAsJsonAsync($"{baseA1}/accounts", new CreateGlAccountRequest(
            "2000", "Liabilitas Header", GlAccountTypes.Liability, GlAccountCategories.HeaderGroup, null, true, false));
        Assert.Equal(HttpStatusCode.Created, hdrRes.StatusCode);
        var hdr = await hdrRes.Content.ReadFromJsonAsync<GlAccountDto>(JsonOptions);
        Assert.False(hdr!.AllowPosting);

        var grniRes = await finA1.PostAsJsonAsync($"{baseA1}/accounts", new CreateGlAccountRequest(
            "2110", "GRNI Clearing", GlAccountTypes.Liability, GlAccountCategories.GrniAccrual, hdr.AccountId, true, true));
        Assert.Equal(HttpStatusCode.Created, grniRes.StatusCode);
        var grni = await grniRes.Content.ReadFromJsonAsync<GlAccountDto>(JsonOptions);

        // 2. Account mapping rejects type mismatch and header accounts
        var badMapType = await finA1.PutAsJsonAsync($"{baseA1}/account-mappings", new UpsertAccountMappingRequest(
            GlMappingPurposes.GrniClearing, world.CashA1Id, "Cash cannot map to Liability GRNI"));
        Assert.Equal(HttpStatusCode.BadRequest, badMapType.StatusCode);

        var badMapHeader = await finA1.PutAsJsonAsync($"{baseA1}/account-mappings", new UpsertAccountMappingRequest(
            GlMappingPurposes.GrniClearing, hdr.AccountId, "Header cannot map"));
        Assert.Equal(HttpStatusCode.BadRequest, badMapHeader.StatusCode);

        var okMap = await finA1.PutAsJsonAsync($"{baseA1}/account-mappings", new UpsertAccountMappingRequest(
            GlMappingPurposes.GrniClearing, grni!.AccountId, "Standard GRNI account"));
        Assert.Equal(HttpStatusCode.OK, okMap.StatusCode);

        // 3. Unbalanced journal & header account & foreign entity account all rejected cleanly
        var unbalanced = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Bad unbalanced", "MANUAL_GL", "MANUAL_JOURNAL", "bad-1", "s04:bad-1",
            [
                new(world.CashA1Id, "Dr Cash", 1_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Equity short", 0m, 999_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, unbalanced.StatusCode);

        var headerPost = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Header post", "MANUAL_GL", "MANUAL_JOURNAL", "bad-2", "s04:bad-2",
            [
                new(hdr.AccountId, "Header debit", 1_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Equity", 0m, 1_000_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.BadRequest, headerPost.StatusCode);

        var foreignAccPost = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Foreign account", "MANUAL_GL", "MANUAL_JOURNAL", "bad-3", "s04:bad-3",
            [
                new(world.ForeignCashB1Id, "Foreign cash", 1_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Equity", 0m, 1_000_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.NotFound, foreignAccPost.StatusCode);

        // 4. Valid balanced journal posts atomically with audit & outbox
        var validRes = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Setoran modal operasional", "MANUAL_GL", "MANUAL_JOURNAL", "man-01", "s04:valid-01",
            [
                new(world.CashA1Id, "Kas bank", 5_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Modal disetor", 0m, 5_000_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.Created, validRes.StatusCode);
        var posted = await validRes.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions);
        Assert.Equal("JRN-2026-11-00001", posted!.JournalNumber);
        Assert.Equal(5_000_000m, posted.TotalDebit);
        Assert.Equal(5_000_000m, posted.TotalCredit);
        Assert.False(posted.IdempotentReplay);

        await using var migDb = fx.CreateMigratorDbContext();
        Assert.True(await migDb.AuditEvents.AnyAsync(a => a.TenantId == world.TenantAId && a.EventType == "GL_JOURNAL_POSTED" && a.SubjectId == posted.JournalId.ToString("D")));
        Assert.True(await migDb.OutboxMessages.AnyAsync(o => o.TenantId == world.TenantAId && o.SourceKey == $"gl:journal:{posted.JournalId:D}"));
    }

    [Fact]
    public async Task S04_API_02_SequentialAndTrueConcurrentDuplicatePostingsStayStrictlyIdempotent()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var finA1 = await CreateSignedInClientAsync(factory, world.FinanceA1Email, world.Password);
        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";

        var seqReq = new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Sequential idempotency test", "GRNI_RECOGNITION", "RECEIPT", "rcpt-01", "grni:rcpt-01",
            [
                new(world.CashA1Id, "Dr Debit", 1_250_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Credit", 0m, 1_250_000m, null, null, null)
            ]);

        var first = await finA1.PostAsJsonAsync($"{baseA1}/journals", seqReq);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions);

        var replay = await finA1.PostAsJsonAsync($"{baseA1}/journals", seqReq);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayDto = await replay.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions);
        Assert.True(replayDto!.IdempotentReplay);
        Assert.Equal(firstDto!.JournalId, replayDto.JournalId);

        // Mutated payload on same source key must conflict 409
        var conflictReq = seqReq with
        {
            Lines =
            [
                new(world.CashA1Id, "Dr Debit", 2_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Credit", 0m, 2_000_000m, null, null, null)
            ]
        };
        var conflictRes = await finA1.PostAsJsonAsync($"{baseA1}/journals", conflictReq);
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);

        // Concurrent 6x parallel posting with identical source key -> strictly 1 journal + 1 set of lines + 1 outbox
        var concurrentReq = new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Concurrent lock race proof", "GRNI_RECOGNITION", "RECEIPT", "rcpt-par-01", "grni:rcpt-par-01",
            [
                new(world.CashA1Id, "Dr Par", 3_000_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Par", 0m, 3_000_000m, null, null, null)
            ]);

        var parallelTasks = Enumerable.Range(0, 6)
            .Select(_ => finA1.PostAsJsonAsync($"{baseA1}/journals", concurrentReq))
            .ToArray();
        var parallelResponses = await Task.WhenAll(parallelTasks);

        Assert.All(parallelResponses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        Assert.Equal(1, parallelResponses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, parallelResponses.Count(r => r.StatusCode == HttpStatusCode.OK));

        var parallelDtos = await Task.WhenAll(parallelResponses.Select(r => r.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions)));
        var winnerId = parallelDtos[0]!.JournalId;
        Assert.All(parallelDtos, dto => Assert.Equal(winnerId, dto!.JournalId));

        await using var migDb = fx.CreateMigratorDbContext();
        Assert.Equal(1, await migDb.JournalEntries.CountAsync(j => j.TenantId == world.TenantAId && j.SourceEventKey == "grni:rcpt-par-01"));
        Assert.Equal(2, await migDb.JournalLines.CountAsync(l => l.TenantId == world.TenantAId && l.JournalId == winnerId));
        Assert.Equal(1, await migDb.OutboxMessages.CountAsync(o => o.TenantId == world.TenantAId && o.SourceKey == $"gl:journal:{winnerId:D}"));
    }

    [Fact]
    public async Task S04_API_03_ClosedPeriodBlocksPostingAndSingleCompensatingReversalPreventsDoubleReversal()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var finA1 = await CreateSignedInClientAsync(factory, world.FinanceA1Email, world.Password);
        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";

        var postRes = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "To be reversed", "MANUAL_GL", "MANUAL_JOURNAL", "rev-src-1", "manual:rev-src-1",
            [
                new(world.CashA1Id, "Dr Cash", 750_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr Capital", 0m, 750_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var original = await postRes.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions);

        // Compensating reversal
        var revRes = await finA1.PostAsJsonAsync($"{baseA1}/journals/{original!.JournalId:D}/reverse", new ReverseJournalRequest(
            new DateOnly(2026, 11, 18), "Salah input nominal modal"));
        Assert.Equal(HttpStatusCode.Created, revRes.StatusCode);
        var reversal = await revRes.Content.ReadFromJsonAsync<JournalDetailDto>(JsonOptions);

        Assert.Equal(JournalEntryKinds.Reversal, reversal!.EntryKind);
        Assert.Equal(original.JournalId, reversal.ReversalOfJournalId);
        Assert.Equal(750_000m, reversal.TotalDebit);
        Assert.Equal(0m, reversal.Lines.Single(l => l.AccountId == world.CashA1Id).DebitAmount);
        Assert.Equal(750_000m, reversal.Lines.Single(l => l.AccountId == world.CashA1Id).CreditAmount);

        // Double reversal & reversal of reversal blocked 409
        var doubleRev = await finA1.PostAsJsonAsync($"{baseA1}/journals/{original.JournalId:D}/reverse", new ReverseJournalRequest(
            new DateOnly(2026, 11, 18), "Double reverse attempt"));
        Assert.Equal(HttpStatusCode.Conflict, doubleRev.StatusCode);

        var revOfRev = await finA1.PostAsJsonAsync($"{baseA1}/journals/{reversal.JournalId:D}/reverse", new ReverseJournalRequest(
            new DateOnly(2026, 11, 18), "Reverse a reversal"));
        Assert.Equal(HttpStatusCode.Conflict, revOfRev.StatusCode);

        // Closing period blocks new postings (409)
        var closeRes = await finA1.PostAsJsonAsync($"{baseA1}/periods/{world.PeriodA1Id:D}/close", new CloseFinancialPeriodRequest(
            "Monthly close Nov 2026", HardLock: false));
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);

        var closedPost = await finA1.PostAsJsonAsync($"{baseA1}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 20), "IDR", "Into closed period", "MANUAL_GL", "MANUAL_JOURNAL", "closed-1", "manual:closed-1",
            [
                new(world.CashA1Id, "Dr", 100_000m, 0m, null, null, null),
                new(world.EquityA1Id, "Cr", 0m, 100_000m, null, null, null)
            ]));
        Assert.Equal(HttpStatusCode.Conflict, closedPost.StatusCode);
    }

    [Fact]
    public async Task S04_API_04_TaxRulesRequireExplicitCodeCompute1112DppEnforceNpwpAndRbac()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await SeedWorldAsync(fx);
        await using var factory = fx.CreateApiFactory();

        using var finA1 = await CreateSignedInClientAsync(factory, world.FinanceA1Email, world.Password);
        using var readA2 = await CreateSignedInClientAsync(factory, world.ReadA2Email, world.Password);
        using var nonFinA1 = await CreateSignedInClientAsync(factory, world.NonFinanceA1Email, world.Password);

        var baseA1 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA1Id:D}/finance";
        var baseA2 = $"/api/workspaces/{world.TenantAId:D}/entities/{world.EntityA2Id:D}/finance";

        // 1. Explicit tax code required
        var missingTax = await finA1.PostAsJsonAsync($"{baseA1}/tax-codes/evaluate-preview", new EvaluateTaxPreviewRequest(
            null, 1_000_000m, new DateOnly(2026, 11, 18), true, true));
        Assert.Equal(HttpStatusCode.BadRequest, missingTax.StatusCode);

        // 2. Create 12% DPP 11/12 & PPh23 requiring NPWP
        var ppnRes = await finA1.PostAsJsonAsync($"{baseA1}/tax-codes", new CreateTaxCodeRequest(
            "PPN-12-DPP1112", "PPN 12% DPP 11/12", TaxFamilies.Ppn, TaxDirections.Input, TaxTreatmentClasses.SpecialDpp1112,
            12m, 11, 12, true, false, world.CashA1Id, new DateOnly(2025, 1, 1), null, "Configurable PMK 131 template"));
        Assert.Equal(HttpStatusCode.Created, ppnRes.StatusCode);
        var ppnDto = await ppnRes.Content.ReadFromJsonAsync<TaxCodeDto>(JsonOptions);

        var pphRes = await finA1.PostAsJsonAsync($"{baseA1}/tax-codes", new CreateTaxCodeRequest(
            "PPH23-SVC-2", "PPh 23 Jasa 2%", TaxFamilies.Pph, TaxDirections.Withholding, TaxTreatmentClasses.WithholdingDeduction,
            2m, 1, 1, true, true, world.EquityA1Id, new DateOnly(2025, 1, 1), null, "Configurable PPh 23 template"));
        Assert.Equal(HttpStatusCode.Created, pphRes.StatusCode);
        var pphDto = await pphRes.Content.ReadFromJsonAsync<TaxCodeDto>(JsonOptions);

        var evalPpn = await finA1.PostAsJsonAsync($"{baseA1}/tax-codes/evaluate-preview", new EvaluateTaxPreviewRequest(
            ppnDto!.TaxCodeId, 1_000_000m, new DateOnly(2026, 11, 18), true, true));
        Assert.Equal(HttpStatusCode.OK, evalPpn.StatusCode);
        var evalPpnBody = await evalPpn.Content.ReadFromJsonAsync<EvaluateTaxPreviewResponse>(JsonOptions);
        Assert.Equal(916_667m, evalPpnBody!.TaxableBaseAmount);
        Assert.Equal(110_000m, evalPpnBody.TaxAmount);
        Assert.Equal(11.00m, evalPpnBody.EffectiveRatePercent);
        Assert.False(evalPpnBody.IsBlocked);

        var evalPphBlocked = await finA1.PostAsJsonAsync($"{baseA1}/tax-codes/evaluate-preview", new EvaluateTaxPreviewRequest(
            pphDto!.TaxCodeId, 5_000_000m, new DateOnly(2026, 11, 18), HasCounterpartyNpwp: false, EvidenceProvided: false));
        Assert.Equal(HttpStatusCode.OK, evalPphBlocked.StatusCode);
        var evalPphBody = await evalPphBlocked.Content.ReadFromJsonAsync<EvaluateTaxPreviewResponse>(JsonOptions);
        Assert.True(evalPphBody!.IsBlocked);
        Assert.Equal("counterparty_npwp_required_for_rate", evalPphBody.BlockedReason);

        // 3. RBAC & Scope checks
        Assert.Equal(HttpStatusCode.OK, (await readA2.GetAsync($"{baseA2}/journals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readA2.PostAsJsonAsync($"{baseA2}/accounts", new CreateGlAccountRequest(
            "9001", "Denied", GlAccountTypes.Asset, GlAccountCategories.General, null, true, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readA2.PostAsJsonAsync($"{baseA2}/journals", new PostJournalRequest(
            new DateOnly(2026, 11, 18), "IDR", "Denied", "MANUAL_GL", "MANUAL_JOURNAL", "x", "x:x", []))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await nonFinA1.GetAsync($"{baseA1}/journals")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await finA1.GetAsync($"/api/workspaces/{world.TenantBId:D}/entities/{world.EntityB1Id:D}/finance/journals")).StatusCode);
    }

    private static async Task SetTenantContextAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid tenantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, true);";
        cmd.Parameters.AddWithValue("tid", tenantId.ToString("D"));
        await cmd.ExecuteScalarAsync();
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

    private static async Task<S04SeedWorld> SeedWorldAsync(PostgresTestDatabaseFixture fx)
    {
        const string pw = "Pass!word1234";
        var (finA1Id, finA1Email, _) = await fx.CreateTestUserAsync("fin.a1.s04@test.local", "Budi Controller", pw);
        var (readA2Id, readA2Email, _) = await fx.CreateTestUserAsync("read.a2.s04@test.local", "Siti Reader", pw);
        var (nonFinA1Id, nonFinA1Email, _) = await fx.CreateTestUserAsync("proc.a1.s04@test.local", "Rina Procurement", pw);
        var (finB1Id, finB1Email, _) = await fx.CreateTestUserAsync("fin.b1.s04@test.local", "Dedi Samudra", pw);

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
        var tenantAId = Guid.NewGuid();
        var entityA1Id = Guid.NewGuid();
        var entityA2Id = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var entityB1Id = Guid.NewGuid();
        var cashA1Id = Guid.NewGuid();
        var eqA1Id = Guid.NewGuid();
        var foreignCashB1Id = Guid.NewGuid();
        var periodA1Id = Guid.NewGuid();

        await using var db = fx.CreateMigratorDbContext();
        db.Tenants.AddRange(
            Tenant.Create(tenantAId, "NUSA", "PT Nusantara Niaga Utama", now),
            Tenant.Create(tenantBId, "SMDR", "CV Samudra Teknik Mandiri", now));

        db.LegalEntities.AddRange(
            LegalEntity.Create(entityA1Id, tenantAId, "NUSA-JKT", "PT Nusantara Distribusi Jakarta", "01.111.111.1-011.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityA2Id, tenantAId, "NUSA-SBY", "PT Nusantara Niaga Surabaya", "01.111.111.1-601.000", "IDR", "ID", "Asia/Jakarta", now),
            LegalEntity.Create(entityB1Id, tenantBId, "SMDR-BDG", "CV Samudra Teknik Bandung", "02.222.222.2-421.000", "IDR", "ID", "Asia/Jakarta", now));

        db.TenantMemberships.AddRange(
            TenantMembership.Create(Guid.NewGuid(), tenantAId, finA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, readA2Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantAId, nonFinA1Id, now),
            TenantMembership.Create(Guid.NewGuid(), tenantBId, finB1Id, now));

        db.EntityAccessGrants.AddRange(
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, finA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, finA1Id, PermissionCodes.FinanceRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, finA1Id, PermissionCodes.FinanceConfigManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, finA1Id, PermissionCodes.FinanceJournalPost, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readA2Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA2Id, readA2Id, PermissionCodes.FinanceRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, nonFinA1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantAId, entityA1Id, nonFinA1Id, PermissionCodes.PartnersRead, now, null, now),

            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, finB1Id, PermissionCodes.WorkspaceView, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, finB1Id, PermissionCodes.FinanceRead, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, finB1Id, PermissionCodes.FinanceConfigManage, now, null, now),
            EntityAccessGrant.Create(Guid.NewGuid(), tenantBId, entityB1Id, finB1Id, PermissionCodes.FinanceJournalPost, now, null, now));

        db.GlAccounts.AddRange(
            GlAccount.Create(cashA1Id, tenantAId, entityA1Id, "1110", "Kas & Bank Utama", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now),
            GlAccount.Create(eqA1Id, tenantAId, entityA1Id, "3100", "Modal Disetor", GlAccountTypes.Equity, GlAccountCategories.EquityCapital, null, true, false, now),
            GlAccount.Create(foreignCashB1Id, tenantBId, entityB1Id, "1110", "Kas Samudra", GlAccountTypes.Asset, GlAccountCategories.CashBank, null, true, false, now));

        db.FinancialPeriods.AddRange(
            FinancialPeriod.Create(periodA1Id, tenantAId, entityA1Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now),
            FinancialPeriod.Create(Guid.NewGuid(), tenantAId, entityA2Id, "2026-11", 2026, 11, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), FinancialPeriodStatuses.Open, now));

        await db.SaveChangesAsync();

        return new S04SeedWorld(
            tenantAId, entityA1Id, entityA2Id, tenantBId, entityB1Id,
            finA1Id, finA1Email, readA2Id, readA2Email, nonFinA1Id, nonFinA1Email, finB1Id, finB1Email,
            cashA1Id, eqA1Id, foreignCashB1Id, periodA1Id, pw);
    }
}
