using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.WhatsApp;

public sealed class WhatsAppNotifyService
{
    private readonly IAppDbContext _db;
    private readonly IWhatsAppSender _sender;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppNotifyService> _logger;

    public WhatsAppNotifyService(
        IAppDbContext db,
        IWhatsAppSender sender,
        IConfiguration config,
        ILogger<WhatsAppNotifyService> logger)
    {
        _db = db;
        _sender = sender;
        _config = config;
        _logger = logger;
    }

    public bool IsEnabled => _sender.IsEnabled;

    public async Task<WhatsAppNotifyResult> NotifyStatusAsync(
        Guid serviceId,
        AuditStatus? statusOverride = null,
        CancellationToken ct = default)
    {
        if (!_sender.IsEnabled)
            return WhatsAppNotifyResult.Fail("WhatsApp no está habilitado. Configurá WhatsApp__* en el .env.");

        var service = await _db.AuditServices
            .Include(s => s.PacienteEntity)
            .FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Prestación no encontrada.");

        var phone = PhoneNormalizer.ToE164Argentina(
            service.PacienteEntity?.Telefono ?? service.TelefonoPaciente);
        if (phone is null)
            return WhatsAppNotifyResult.Fail("El paciente no tiene un teléfono válido para WhatsApp.");

        var status = statusOverride ?? service.Status;
        var template = _config["WhatsApp:StatusTemplateName"] ?? "auditart_estado_actualizado";
        var lang = _config["WhatsApp:StatusTemplateLanguage"] ?? "es_AR";
        var paciente = service.PacienteEntity?.Nombre ?? service.Paciente;
        var statusLabel = status.ToString();

        var log = WhatsAppOutboundLog.Create(
            serviceId,
            phone,
            template,
            $"Estado {statusLabel} — {paciente}");
        _db.Add(log);
        await _db.SaveChangesAsync(ct);

        var result = await _sender.SendTemplateAsync(
            phone,
            template,
            lang,
            [paciente, statusLabel, service.Numero.ToString()],
            ct);

        if (result.Ok)
            log.MarkSent(result.ProviderMessageId);
        else
            log.MarkFailed(result.Error ?? "Error desconocido");

        await _db.SaveChangesAsync(ct);

        if (!result.Ok)
            return WhatsAppNotifyResult.Fail(result.Error ?? "No se pudo enviar el WhatsApp.");

        return WhatsAppNotifyResult.Ok(log.Id, phone);
    }

    public bool ShouldAutoNotify(AuditStatus status)
    {
        if (!_sender.IsEnabled) return false;
        var raw = _config["WhatsApp:NotifyOnStatuses"] ?? "Amarillo,Verde";
        var set = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();
        return set.Contains(status.ToString().ToLowerInvariant());
    }

    public async Task TryAutoNotifyAsync(Guid serviceId, AuditStatus status, CancellationToken ct)
    {
        if (!ShouldAutoNotify(status)) return;
        try
        {
            var result = await NotifyStatusAsync(serviceId, status, ct);
            if (!result.Success)
                _logger.LogWarning("WhatsApp auto-notify falló para {ServiceId}: {Error}", serviceId, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp auto-notify excepción para {ServiceId}", serviceId);
        }
    }
}

public sealed record WhatsAppNotifyResult(bool Success, Guid? LogId, string? Phone, string? Error)
{
    public static WhatsAppNotifyResult Ok(Guid logId, string phone) => new(true, logId, phone, null);
    public static WhatsAppNotifyResult Fail(string error) => new(false, null, null, error);
}
