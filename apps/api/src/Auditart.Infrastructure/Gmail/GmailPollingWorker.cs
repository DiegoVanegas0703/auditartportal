using Auditart.Application.Abstractions;
using Auditart.Application.Triage;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Gmail;

public sealed class GmailPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGmailChannelRegistry _channels;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailPollingWorker> _logger;

    public GmailPollingWorker(
        IServiceScopeFactory scopeFactory,
        IGmailChannelRegistry channels,
        IConfiguration configuration,
        ILogger<GmailPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _channels = channels;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection(GmailOptions.SectionName);
        if (!section.GetValue("PollingEnabled", true))
        {
            _logger.LogInformation("Sincronización automática de Gmail deshabilitada (Gmail:PollingEnabled=false).");
            return;
        }

        if (_channels.GetEnabledChannels().Count == 0)
        {
            _logger.LogInformation("Sincronización automática de Gmail deshabilitada (ningún canal activo).");
            return;
        }

        var minutes = Math.Clamp(section.GetValue("PollingIntervalMinutes", 2), 1, 60);
        var interval = TimeSpan.FromMinutes(minutes);
        _logger.LogInformation("Sincronización automática de Gmail cada {Minutes} min.", minutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            var hasMore = await SyncAsync(stoppingToken);

            try
            {
                await Task.Delay(hasMore ? TimeSpan.FromSeconds(10) : interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<bool> SyncAsync(CancellationToken cancellationToken)
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
            foreach (var error in result.Errors.Take(5))
                _logger.LogWarning("Gmail sync error: {Error}", error);
            if (result.HasMore)
                _logger.LogInformation("Quedan correos pendientes; se continúa en 10 s.");
            return result.HasMore;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
        catch (TokenResponseException ex) when (ex.Error?.Error == "invalid_grant")
        {
            _logger.LogError(
                "Gmail rechazó el refresh token (invalid_grant): venció o fue revocado. " +
                "Generá uno nuevo (docs/GMAIL-REFRESH-TOKEN.md) y actualizá Gmail__RefreshToken en .env. " +
                "Con la app OAuth en modo Testing, Google lo vence a los 7 días.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la sincronización automática de Gmail.");
        }

        return false;
    }
}
