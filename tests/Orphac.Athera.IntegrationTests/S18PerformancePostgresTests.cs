using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Api.Contracts;
using Orphac.Athera.IntegrationTests.Infrastructure;
using Orphac.Athera.Modules.GeneralLedger;
using Xunit;
using Xunit.Abstractions;

namespace Orphac.Athera.IntegrationTests;

[CollectionDefinition("S18 latency", DisableParallelization = true)]
public sealed class S18LatencyCollection;

[Collection("S18 latency")]
public sealed class S18PerformancePostgresTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITestOutputHelper _output;

    public S18PerformancePostgresTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task S18_PERF_01_ReadAndPostingLatencyAtEngineeringChosenVolume()
    {
        await using var fx = new PostgresTestDatabaseFixture();
        await fx.InitializeAsync();
        var world = await S18ReleaseCandidatePostgresTests.SeedRcWorldAsync(fx);

        // Seed 2,000 posted journals (2 lines each, Dr cash / Cr revenue, amount 1,000 * i) in batches
        await using (var db = fx.CreateMigratorDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            const int batchSize = 250;
            for (var batchStart = 1; batchStart <= 2000; batchStart += batchSize)
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                for (var i = batchStart; i < batchStart + batchSize && i <= 2000; i++)
                {
                    var amount = 1_000m * i;
                    var res = await GlPostingKernel.PostJournalAsync(
                        db,
                        new GlPostJournalCommand(
                            world.TenantAId, world.EntityA1Id, "IDR",
                            new DateOnly(2026, 11, ((i - 1) % 30) + 1), "IDR",
                            $"Perf seed journal {i}",
                            "FINANCE", "MANUAL_JOURNAL", $"perf-seed-{i:D4}", $"perf-seed-{i:D4}",
                            [
                                new GlJournalLineInput(world.CashAccountId, "Dr", amount, 0m),
                                new GlJournalLineInput(world.RevenueAccountId, "Cr", 0m, amount)
                            ],
                            world.ControllerId, now),
                        CancellationToken.None);
                    Assert.True(res.Succeeded);
                }
                await tx.CommitAsync();
            }
        }

        using var factory = fx.CreateApiFactory();
        using var ctrlClient = await S18ReleaseCandidatePostgresTests.SignInAndGetCsrfAsync(factory, world.ControllerEmail, world.Password);
        var baseUrl = $"/api/workspaces/{world.TenantAId}/entities/{world.EntityA1Id}";
        var overviewUrl = $"{baseUrl}/reports/overview?periodId={world.PeriodNovId}";

        static async Task<long> TimedAsync(Func<Task<HttpResponseMessage>> send)
        {
            var sw = Stopwatch.StartNew();
            using var res = await send();
            Assert.True(res.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
                $"Timed call failed with {(int)res.StatusCode}");
            _ = await res.Content.ReadAsStringAsync();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // 3 warm-up + 40 measured GET overview
        for (var w = 0; w < 3; w++)
        {
            _ = await TimedAsync(() => ctrlClient.GetAsync(overviewUrl));
        }
        var readTimes = new List<long>(40);
        for (var i = 0; i < 40; i++)
        {
            readTimes.Add(await TimedAsync(() => ctrlClient.GetAsync(overviewUrl)));
        }

        async Task<long> JournalsInPeriodAsync()
        {
            await using var db = fx.CreateMigratorDbContext();
            return await db.JournalEntries.LongCountAsync(
                j => j.TenantId == world.TenantAId && j.LegalEntityId == world.EntityA1Id);
        }

        var readStats = Stats(readTimes);
        _output.WriteLine(
            $"S18_PERF_01 READ n=40 min={readStats.Min} median={readStats.Median} p95={readStats.P95} max={readStats.Max} ms (journals in period={await JournalsInPeriodAsync()})");

        // 3 warm-up + 40 measured POST journals (unique keys, balanced 2-line)
        HttpRequestMessage PostJournal(int i) => new(HttpMethod.Post, $"{baseUrl}/finance/journals")
        {
            Content = JsonContent.Create(new PostJournalRequest(
                new DateOnly(2026, 11, 15),
                "IDR",
                $"Perf measured journal {i}",
                "FINANCE",
                "MANUAL_JOURNAL",
                $"perf-post-{i:D4}",
                $"perf-post-{i:D4}",
                [
                    new JournalLineInputDto(world.CashAccountId, "Dr", 1_000m, 0m, null, null, null),
                    new JournalLineInputDto(world.RevenueAccountId, "Cr", 0m, 1_000m, null, null, null)
                ]), options: JsonOptions)
        };

        for (var w = 0; w < 3; w++)
        {
            var wIdx = 9000 + w;
            _ = await TimedAsync(() => ctrlClient.SendAsync(PostJournal(wIdx)));
        }
        var postTimes = new List<long>(40);
        for (var i = 0; i < 40; i++)
        {
            var idx = i;
            postTimes.Add(await TimedAsync(() => ctrlClient.SendAsync(PostJournal(idx))));
        }

        var postStats = Stats(postTimes);
        _output.WriteLine(
            $"S18_PERF_01 POST n=40 min={postStats.Min} median={postStats.Median} p95={postStats.P95} max={postStats.Max} ms (journals in period={await JournalsInPeriodAsync()})");

        Assert.True(readStats.P95 < 2000, $"Read p95 {readStats.P95}ms exceeds 2000ms target");
        Assert.True(postStats.P95 < 3000, $"Post p95 {postStats.P95}ms exceeds 3000ms target");

        var finalOverview = await ctrlClient.GetFromJsonAsync<AccountingReportsOverviewResponse>(overviewUrl, JsonOptions);
        Assert.NotNull(finalOverview);
        Assert.True(finalOverview!.TrialBalance.IsBalanced);
    }

    private static (long Min, decimal Median, long P95, long Max) Stats(List<long> samples)
    {
        var sorted = samples.OrderBy(x => x).ToList();
        var p95 = sorted[(int)Math.Ceiling(0.95 * sorted.Count) - 1];
        var median = (sorted[19] + sorted[20]) / 2m;
        return (sorted[0], median, p95, sorted[^1]);
    }
}
