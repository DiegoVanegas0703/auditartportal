using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Pacientes;
using Auditart.Application.Sla;
using Auditart.Application.Triage;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/triage")]
public class TriageController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly GmailIngestionService _gmailIngestion;
    private readonly SlaRuleService _slaRules;
    private readonly IGmailChannelRegistry _channels;
    private readonly PacienteService _pacientes;
    private readonly ILogger<TriageController> _logger;

    public TriageController(
        IAppDbContext db,
        GmailIngestionService gmailIngestion,
        SlaRuleService slaRules,
        IGmailChannelRegistry channels,
        PacienteService pacientes,
        ILogger<TriageController> logger)
    {
        _db = db;
        _gmailIngestion = gmailIngestion;
        _slaRules = slaRules;
        _channels = channels;
        _pacientes = pacientes;
        _logger = logger;
    }

    [HttpGet("emails")]
    public async Task<ActionResult<PagedEmailResponse>> ListPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] bool ignored = false,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);
        var normalizedTag = string.IsNullOrWhiteSpace(tag)
            ? null
            : tag.Trim().ToLowerInvariant();

        var actionable = _db.IncomingEmails.Where(e => !e.IsAssigned);
        var pendingCount = await actionable.CountAsync(e => !e.IsIgnored, ct);
        var ignoredCount = await actionable.CountAsync(e => e.IsIgnored, ct);
        var tagSets = await actionable
            .Where(e => e.Tags.Count > 0)
            .Select(e => e.Tags)
            .ToListAsync(ct);
        var availableTags = tagSets
            .SelectMany(tags => tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToArray();

        var query = actionable.Where(e => e.IsIgnored == ignored);
        if (normalizedTag is not null)
            query = query.Where(e => e.Tags.Contains(normalizedTag));

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new IncomingEmailDto(
                e.Id,
                e.FromAddress,
                e.Subject,
                e.Body,
                e.ReceivedAtUtc,
                e.AttachmentCount,
                e.SuggestedQueue,
                e.SuggestedServiceType,
                e.SuggestedArt,
                e.SuggestedPatientName,
                e.IsAssigned,
                e.IsIgnored,
                e.Tags,
                e.Attachments
                    .OrderBy(a => a.FileName)
                    .Select(a => new IncomingAttachmentDto(
                        a.Id,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes))
                    .ToList()))
            .ToListAsync(ct);

        return Ok(new PagedEmailResponse(
            items,
            page,
            pageSize,
            total,
            totalPages,
            pendingCount,
            ignoredCount,
            availableTags));
    }

    [HttpPost("sync-gmail")]
    public async Task<ActionResult<GmailSyncResult>> SyncGmail(CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        try
        {
            return Ok(await _gmailIngestion.SyncUnreadAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("seed-demo-emails")]
    public async Task<IActionResult> SeedDemoEmails(CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        if (await _db.IncomingEmails.AnyAsync(e => !e.IsAssigned && !e.IsIgnored, ct))
            return Ok(new { message = "Ya hay emails pendientes.", created = 0 });

        var demos = new[]
        {
            IncomingEmail.Create(
                $"demo-{Guid.NewGuid():N}",
                "art.lacaja@ejemplo.com",
                "Solicitud auditoría consultorio - Siniestro 45231",
                "Estimados, solicitamos auditoría de consultorio para Juan Pérez, DNI 28.456.789.",
                DateTime.UtcNow.AddHours(-4),
                2,
                AuditQueue.General,
                ServiceType.Consultorio,
                "La Caja ART",
                "Juan Pérez"),
            IncomingEmail.Create(
                $"demo-{Guid.NewGuid():N}",
                "gestion@provart.com.ar",
                "Telemedicina - Control post operatorio",
                "Requerimos teleconsulta para María López, DNI 31.234.567.",
                DateTime.UtcNow.AddHours(-3),
                1,
                AuditQueue.Telemedicina,
                ServiceType.Telemedicina,
                "Provincia ART",
                "María López"),
            IncomingEmail.Create(
                $"demo-{Guid.NewGuid():N}",
                "cronicos@galiciaseguros.com",
                "Paciente crónico - Reevaluación trimestral",
                "Solicitamos reevaluación de Roberto Sánchez, DNI 15.678.901.",
                DateTime.UtcNow.AddHours(-2),
                3,
                AuditQueue.Cronicos,
                ServiceType.Consultorio,
                "Galicia Seguros",
                "Roberto Sánchez"),
            IncomingEmail.Create(
                $"demo-{Guid.NewGuid():N}",
                "auditoria@experta.com.ar",
                "URGENTE - Auditoría en domicilio",
                "Solicitud urgente de auditoría en domicilio para Carlos Mendoza.",
                DateTime.UtcNow.AddHours(-1),
                1,
                AuditQueue.General,
                ServiceType.Domicilio,
                "Experta ART",
                "Carlos Mendoza"),
        };

        foreach (var email in demos)
            _db.Add(email);

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Emails de demo creados.", created = demos.Length });
    }

    [HttpPost("emails/{emailId:guid}/ignore")]
    public async Task<IActionResult> Ignore(Guid emailId, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        var email = await _db.IncomingEmails.FirstOrDefaultAsync(e => e.Id == emailId, ct);
        if (email is null) return NotFound();
        if (email.IsAssigned)
            return Conflict(new { error = "El email ya fue derivado." });

        email.Ignore(GetUserId());
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("emails/{emailId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid emailId, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        var email = await _db.IncomingEmails.FirstOrDefaultAsync(e => e.Id == emailId, ct);
        if (email is null) return NotFound();

        email.Restore();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("emails/{emailId:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<string>>> UpdateTags(
        Guid emailId,
        [FromBody] UpdateEmailTagsRequest request,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        var email = await _db.IncomingEmails.FirstOrDefaultAsync(e => e.Id == emailId, ct);
        if (email is null) return NotFound();

        var previous = email.Tags.ToList();
        email.SetTags(request.Tags ?? []);
        await _db.SaveChangesAsync(ct);

        var next = email.Tags;
        var added = next.Except(previous, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = previous.Except(next, StringComparer.OrdinalIgnoreCase).ToList();
        if ((added.Count > 0 || removed.Count > 0) &&
            !string.IsNullOrWhiteSpace(email.GmailMessageId))
        {
            try
            {
                await _channels.GetInbox(EmailChannel.General)
                    .ModifyMessageLabelsByNameAsync(email.GmailMessageId, added, removed, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudieron sincronizar tags a Gmail para el email legacy {EmailId}",
                    emailId);
            }
        }

        return Ok(email.Tags);
    }

    [HttpPost("emails/{emailId:guid}/assign")]
    public async Task<ActionResult<AuditServiceDto>> Assign(
        Guid emailId,
        [FromBody] AssignEmailRequest request,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        var email = await _db.IncomingEmails.FirstOrDefaultAsync(e => e.Id == emailId, ct);
        if (email is null) return NotFound();
        if (email.IsAssigned) return Conflict(new { error = "El email ya fue derivado." });
        if (email.IsIgnored)
            return Conflict(new { error = "Restaurá el email antes de derivarlo." });

        var operador = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.OperadorId && u.IsActive, ct);
        if (operador is null) return BadRequest(new { error = "Operador inválido." });

        var nextNumero = await _db.AuditServices.AnyAsync(ct)
            ? await _db.AuditServices.MaxAsync(s => s.Numero, ct) + 1
            : 1001;

        var defaultUrgency = email.Subject.Contains("URGENT", StringComparison.OrdinalIgnoreCase)
            ? UrgencyLevel.Critica
            : UrgencyLevel.Alta;

        var paciente = FirstNonEmpty(request.Paciente, email.SuggestedPatientName) ?? "Sin identificar";
        var art = FirstNonEmpty(request.Art, email.SuggestedArt) ?? "Por definir";

        var service = AuditService.CreateFromTriage(
            nextNumero,
            paciente,
            art,
            request.TipoServicio ?? email.SuggestedServiceType ?? ServiceType.Consultorio,
            request.Queue,
            operador.Id,
            request.Urgency ?? defaultUrgency,
            email.Id,
            especialidad: FirstNonEmpty(request.Especialidad),
            dni: FirstNonEmpty(request.Dni),
            notas: $"Derivado desde email: {email.Subject}");

        var rojoDeadline = await _slaRules.ComputeDeadlineAsync(
            request.Queue,
            AuditStatus.Rojo,
            DateTime.UtcNow,
            ct);
        service.ApplySlaDeadline(rojoDeadline);
        await _pacientes.LinkServiceAsync(service, ct);

        _db.Add(service);
        email.MarkAssigned(GetUserId(), service.Id);

        var attachments = await _db.ServiceAttachments
            .Where(attachment => attachment.IncomingEmailId == email.Id)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
            attachment.LinkToService(service.Id);

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(service, operador.Name));
    }

    private bool IsJefaturaOrAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return role is nameof(UserRole.Admin) or nameof(UserRole.Jefatura);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static AuditServiceDto ToDto(AuditService s, string? operadorName) => new(
        s.Id, s.Numero, s.Paciente, s.Dni, s.Art, s.TipoServicio, s.Especialidad,
        s.Profesional, s.OperadorId, operadorName, s.Queue, s.Status, s.Urgency,
        s.FechaIngresoUtc, s.FechaTurnoUtc, s.FechaConsultaUtc, s.SlaDeadlineUtc,
        s.ValorPactado, s.PresupuestoEnviado, s.AutorizacionArt, s.Autofisica, s.Notas);
}

public sealed record PagedEmailResponse(
    IReadOnlyList<IncomingEmailDto> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages,
    int PendingCount,
    int IgnoredCount,
    IReadOnlyList<string> AvailableTags);

public sealed record IncomingEmailDto(
    Guid Id,
    string From,
    string Subject,
    string Body,
    DateTime ReceivedAtUtc,
    int AttachmentCount,
    AuditQueue? SuggestedQueue,
    ServiceType? SuggestedServiceType,
    string? SuggestedArt,
    string? SuggestedPatientName,
    bool IsAssigned,
    bool IsIgnored,
    IReadOnlyList<string> Tags,
    IReadOnlyList<IncomingAttachmentDto> Attachments);

public sealed record IncomingAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record AssignEmailRequest(
    AuditQueue Queue,
    Guid OperadorId,
    string? Paciente = null,
    string? Dni = null,
    string? Art = null,
    ServiceType? TipoServicio = null,
    string? Especialidad = null,
    UrgencyLevel? Urgency = null);

public sealed record UpdateEmailTagsRequest(IReadOnlyList<string>? Tags);

public sealed record AuditServiceDto(
    Guid Id,
    int Numero,
    string Paciente,
    string? Dni,
    string Art,
    ServiceType TipoServicio,
    string? Especialidad,
    string? Profesional,
    Guid? OperadorId,
    string? OperadorName,
    AuditQueue Queue,
    AuditStatus Status,
    UrgencyLevel Urgency,
    DateTime FechaIngresoUtc,
    DateTime? FechaTurnoUtc,
    DateTime? FechaConsultaUtc,
    DateTime? SlaDeadlineUtc,
    decimal? ValorPactado,
    bool PresupuestoEnviado,
    bool AutorizacionArt,
    bool Autofisica,
    string? Notas);
