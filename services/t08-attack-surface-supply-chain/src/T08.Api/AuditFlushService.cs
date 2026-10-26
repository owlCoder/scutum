using T08.Infrastructure.Audit;

namespace T08.Api;

/// <summary>
/// Periodično prazni audit red ka T04 da događaji ne čekaju sledeći audit ili poziv <c>/health/audit</c> (nalaz В.2).
/// Ne zamenjuje trajni outbox: restart procesa i dalje gubi ono što je u redu.
/// </summary>
public sealed class AuditFlushService(BufferedAuditSink audit, TimeSpan interval, ILogger<AuditFlushService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await audit.FlushAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Greška jednog pokušaja ne sme da ugasi flush; sledeći tik pokušava ponovo.
                    logger.LogWarning(ex, "Audit flush nije uspeo; ponovni pokušaj za {Interval}.", interval);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // gašenje hosta
        }
    }
}
