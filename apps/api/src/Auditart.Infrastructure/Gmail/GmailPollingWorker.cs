using Auditart.Application.Abstractions;
using Auditart.Application.Triage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Gmail;

public sealed class GmailPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGmailChannelRegistry _channels;
    private readonly ILogger<GmailPollingWorker> _logger;

    public GmailPollingWorker(
        IServiceScopeFactory scopeFactory,
        IGmailChannelRegistry channels,
        ILogger<GmailPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _channels = channels;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channels.GetEnabledChannels().Count == 0)
        {
            _logger.LogInformation("Sincronización automática de Gmail deshabilitada (ningún canal activo).");
            return;
        }
        var interval = TimeSpan.FromMinutes(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<GmailIngestionService>();
            var result = await ingestion.SyncUnreadAsync(cancellationToken);

            _logger.LogInformation(
                "Gmail sincronizado: {Fetched} encontrados, {Inserted} importados, " +
                "{Skipped} omitidos, {Failed} errores.",
                result.Fetched,
                result.Inserted,
                result.Skipped,
                result.Failed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la sincronización automática de Gmail.");
        }
    }
}
