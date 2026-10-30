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

    public async Task<WhatsAppTurnoPreviewDto> PreviewTurnoAsync(
        Guid serviceId,
        Guid notifierUserId,
        CancellationToken ct = default)
    {
        var service = await LoadServiceForTurnoAsync(serviceId, ct);
        var notifier = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == notifierUserId, ct);

        var patientPhone = PhoneNormalizer.ToE164Argentina(
            service.PacienteEntity?.Telefono ?? service.TelefonoPaciente);
        var kind = WhatsAppTurnoMessage.KindFor(service);
        var template = await GetTemplateBodyAsync(kind, ct);
        var body = WhatsAppTurnoMessage.Build(service, notifier, template, out var contactLink);

        var canSend = true;
        string? block = null;
        if (!_sender.IsEnabled)
        {
            canSend = false;
            block = "WhatsApp no está habilitado en el servidor.";
        }
        else if (patientPhone is null)
        {
            canSend = false;
            block = "El paciente no tiene un teléfono válido para WhatsApp.";
        }
        else if (service.FechaTurnoUtc is null)
        {
            canSend = false;
            block = "Este caso aún no tiene fecha de turno. Coordiná el turno (Amarillo) primero.";
        }
        else if (string.IsNullOrWhiteSpace(notifier?.WhatsAppPhone))
        {
            canSend = false;
            block = "Configurá tu WhatsApp en Mi cuenta para incluir el link de contacto.";
        }

        return new WhatsAppTurnoPreviewDto(
            patientPhone,
            body,
            contactLink,
            notifier?.Name,
            canSend,
            block,
            service.FechaTurnoUtc,
            service.Prestador?.Domicilio,
            service.Prestador?.Nombre ?? service.Profesional,
            service.PacienteEntity?.Nombre ?? service.Paciente,
            string.IsNullOrWhiteSpace(service.Especialidad)
                ? service.TipoServicio.ToString()
                : service.Especialidad!,
            kind,
            template);
    }

    public async Task<WhatsAppNotifyResult> NotifyTurnoAsync(
        Guid serviceId,
        Guid notifierUserId,
        string? customBody = null,
        CancellationToken ct = default)
    {
        var preview = await PreviewTurnoAsync(serviceId, notifierUserId, ct);
        if (!string.IsNullOrWhiteSpace(customBody))
            preview = preview with { MessageBody = customBody.Trim() };
        if (!preview.CanSend)
            return WhatsAppNotifyResult.Fail(preview.BlockReason ?? "No se puede enviar.");

        var phone = preview.PatientPhoneE164!;
        var log = WhatsAppOutboundLog.Create(
            serviceId,
            phone,
            "turno_asignado_texto",
            preview.MessageBody.Length > 480
                ? preview.MessageBody[..480]
                : preview.MessageBody);
        _db.Add(log);
        await _db.SaveChangesAsync(ct);

        var result = await _sender.SendTextAsync(phone, preview.MessageBody, ct);
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
        var raw = _config["WhatsApp:NotifyOnStatuses"] ?? "Amarillo";
        var set = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();
        return set.Contains(status.ToString().ToLowerInvariant());
    }

    public async Task TryAutoNotifyAsync(
        Guid serviceId,
        AuditStatus status,
        Guid notifierUserId,
        CancellationToken ct)
    {
        if (!ShouldAutoNotify(status)) return;
        if (status != AuditStatus.Amarillo) return;
        try
        {
            var result = await NotifyTurnoAsync(serviceId, notifierUserId, null, ct);
            if (!result.Success)
                _logger.LogWarning("WhatsApp turno auto-notify falló para {ServiceId}: {Error}", serviceId, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp turno auto-notify excepción para {ServiceId}", serviceId);
        }
    }

    private async Task<AuditService> LoadServiceForTurnoAsync(Guid serviceId, CancellationToken ct)
    {
        return await _db.AuditServices
            .Include(s => s.PacienteEntity)
            .Include(s => s.Prestador)
            .FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Prestación no encontrada.");
    }

    public async Task<IReadOnlyList<WhatsAppTemplateDto>> ListTemplatesAsync(CancellationToken ct)
    {
        var stored = await _db.WhatsAppTemplates.AsNoTracking().ToListAsync(ct);
        return new[] { WhatsAppTurnoMessage.KindTurno, WhatsAppTurnoMessage.KindTelemedicina }
            .Select(key =>
            {
                var body = stored.FirstOrDefault(t => t.Key == key)?.Body
                    ?? WhatsAppTurnoMessage.DefaultFor(key);
                return new WhatsAppTemplateDto(key, TitleFor(key), body, WhatsAppTurnoMessage.DefaultFor(key));
            })
            .ToList();
    }

    public async Task<WhatsAppTemplateDto> SaveTemplateAsync(string key, string body, CancellationToken ct)
    {
        key = NormalizeKey(key);
        var entity = await _db.WhatsAppTemplates.FirstOrDefaultAsync(t => t.Key == key, ct);
        if (entity is null)
        {
            entity = WhatsAppTemplate.Create(key, body);
            _db.Add(entity);
        }
        else
        {
            entity.SetBody(body);
        }

        await _db.SaveChangesAsync(ct);
        return new WhatsAppTemplateDto(key, TitleFor(key), entity.Body, WhatsAppTurnoMessage.DefaultFor(key));
    }

    private async Task<string> GetTemplateBodyAsync(string kind, CancellationToken ct)
    {
        var stored = await _db.WhatsAppTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == kind, ct);
        return string.IsNullOrWhiteSpace(stored?.Body)
            ? WhatsAppTurnoMessage.DefaultFor(kind)
            : stored.Body;
    }

    private static string NormalizeKey(string key)
    {
        var k = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (k is not (WhatsAppTurnoMessage.KindTurno or WhatsAppTurnoMessage.KindTelemedicina))
            throw new ArgumentException("Plantilla inválida. Usá turno o telemedicina.");
        return k;
    }

    private static string TitleFor(string key) =>
        WhatsAppTurnoMessage.IsTelemedicina(key)
            ? "Telemedicina"
            : "Turno presencial";
}

public sealed record WhatsAppTurnoPreviewDto(
    string? PatientPhoneE164,
    string MessageBody,
    string? ContactWhatsAppLink,
    string? NotifierName,
    bool CanSend,
    string? BlockReason,
    DateTime? FechaTurnoUtc,
    string? Direccion,
    string? Medico,
    string PacienteNombre,
    string Motivo,
    string Kind,
    string TemplateBody);

public sealed record WhatsAppTemplateDto(string Key, string Title, string Body, string DefaultBody);

public sealed record WhatsAppNotifyResult(bool Success, Guid? LogId, string? Phone, string? Error)
{
    public static WhatsAppNotifyResult Ok(Guid logId, string phone) => new(true, logId, phone, null);
    public static WhatsAppNotifyResult Fail(string error) => new(false, null, null, error);
}
