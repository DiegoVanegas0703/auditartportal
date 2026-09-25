using Auditart.Application.Sla;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auditart.Infrastructure.Sla;

public sealed class SlaAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaAlertWorker> _logger;
    private readonly SlaAlertOptions _options;

    public SlaAlertWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SlaAlertOptions> options,
        ILogger<SlaAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Worker de alertas SLA deshabilitado.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        // Seed reglas al arrancar
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SlaRuleService>().EnsureDefaultsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron asegurar reglas SLA por defecto.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken);
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

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var rules = scope.ServiceProvider.GetRequiredService<SlaRuleService>();
            var alerts = scope.ServiceProvider.GetRequiredService<InAppAlertService>();

            var backfilled = await rules.BackfillMissingDeadlinesAsync(cancellationToken);
            if (backfilled > 0)
                _logger.LogInformation("SLA backfill: {Count} servicios actualizados", backfilled);

            var created = await alerts.ProcessDueAlertsAsync(cancellationToken);
            if (created > 0)
                _logger.LogInformation("Alertas SLA creadas: {Count}", created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando alertas SLA.");
        }
    }
}
