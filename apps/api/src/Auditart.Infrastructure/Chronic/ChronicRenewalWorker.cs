using Auditart.Application.Chronic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Auditart.Infrastructure.Chronic;

public sealed class ChronicRenewalWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChronicRenewalWorker> _logger;
    private readonly ChronicRenewalOptions _options;

    public ChronicRenewalWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ChronicRenewalOptions> options,
        ILogger<ChronicRenewalWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Renovación automática de crónicos deshabilitada.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.IntervalMinutes));

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
            var chronicService = scope.ServiceProvider.GetRequiredService<ChronicServiceService>();
            var renewed = await chronicService.ProcessDueRenewalsAsync(cancellationToken);

            if (renewed > 0)
                _logger.LogInformation("Crónicos renovados automáticamente: {Count}", renewed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar renovaciones de crónicos.");
        }
    }
}
