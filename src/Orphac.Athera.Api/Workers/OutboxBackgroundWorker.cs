using Orphac.Athera.Modules.Integration;

namespace Orphac.Athera.Api.Workers;

/// <summary>
/// Drains tenant-signaled outbox messages inside per-tenant PostgreSQL RLS transactions.
/// Skipped in the "Testing" environment so integration suites execute deterministically.
/// </summary>
public sealed class OutboxBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    ITenantOutboxSignal signal,
    IHostEnvironment environment,
    ILogger<OutboxBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        await foreach (var tenantId in signal.ReadTenantSignalsAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<TenantOutboxProcessor>();
                await processor.ProcessPendingForTenantAsync(tenantId, null, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transient outbox processing error for tenant {TenantId}", tenantId);
            }
        }
    }
}
