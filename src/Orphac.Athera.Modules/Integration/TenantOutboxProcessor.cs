using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Orphac.Athera.Modules.Persistence;

namespace Orphac.Athera.Modules.Integration;

public sealed record OutboxProcessingSummary(int ProcessedCount, int FailedCount);

public interface ITenantOutboxSignal
{
    void NotifyTenant(Guid tenantId);
    IAsyncEnumerable<Guid> ReadTenantSignalsAsync(CancellationToken ct);
}

public sealed class TenantOutboxSignal : ITenantOutboxSignal
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public void NotifyTenant(Guid tenantId)
    {
        if (tenantId != Guid.Empty)
        {
            _channel.Writer.TryWrite(tenantId);
        }
    }

    public IAsyncEnumerable<Guid> ReadTenantSignalsAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Processes pending tenant outbox messages inside an explicit transaction-local
/// PostgreSQL RLS scope (app.tenant_id) with idempotent inbox receipt tracking.
/// </summary>
public sealed class TenantOutboxProcessor(AtheraDbContext db, TimeProvider? timeProvider = null)
{
    public const string DefaultConsumerName = "athera.internal.outbox";

    private readonly AtheraDbContext _db = db;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<OutboxProcessingSummary> ProcessPendingForTenantAsync(
        Guid tenantId,
        Func<OutboxMessage, CancellationToken, Task>? handler,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return new OutboxProcessingSummary(0, 0);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var tidStr = tenantId.ToString("D");
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {tidStr}, true), set_config('app.actor_user_id', '', true)",
            ct);

        var nowUtc = _clock.GetUtcNow();
        var batch = await _db.OutboxMessages
            .FromSqlInterpolated(
                $"""
                SELECT * FROM integration.outbox_messages
                WHERE tenant_id = {tenantId}
                  AND status IN ('PENDING', 'FAILED')
                  AND next_attempt_at_utc <= {nowUtc}
                  AND attempt_count < 5
                ORDER BY created_at_utc
                LIMIT 20
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            await tx.CommitAsync(ct);
            return new OutboxProcessingSummary(0, 0);
        }

        var processed = 0;
        var failed = 0;

        foreach (var message in batch)
        {
            var alreadyReceipted = await _db.InboxReceipts
                .AsNoTracking()
                .AnyAsync(
                    r => r.TenantId == tenantId
                        && r.ConsumerName == DefaultConsumerName
                        && r.MessageId == message.MessageId,
                    ct);

            if (alreadyReceipted)
            {
                message.MarkProcessed(nowUtc);
                continue;
            }

            await tx.CreateSavepointAsync("sp_outbox_msg", ct);
            try
            {
                if (handler is not null)
                {
                    await handler(message, ct);
                }

                _db.InboxReceipts.Add(InboxReceipt.Record(
                    Guid.NewGuid(),
                    tenantId,
                    DefaultConsumerName,
                    message.MessageId,
                    nowUtc));
                message.MarkProcessed(nowUtc);
                await _db.SaveChangesAsync(ct);
                await tx.ReleaseSavepointAsync("sp_outbox_msg", ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await tx.RollbackToSavepointAsync("sp_outbox_msg", ct);
                foreach (var added in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                {
                    added.State = EntityState.Detached;
                }

                message.MarkFailed(ex.Message, nowUtc);
                await _db.SaveChangesAsync(ct);
                failed++;
            }
        }

        await tx.CommitAsync(ct);
        return new OutboxProcessingSummary(processed, failed);
    }
}
