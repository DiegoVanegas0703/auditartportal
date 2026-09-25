using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Chronic;
using Auditart.Application.Sla;
using Auditart.Application.Triage;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly EmailConversationService _conversations;
    private readonly ChronicServiceService _chronic;
    private readonly SlaRuleService _slaRules;
    private readonly IObjectStorage _storage;

    public ServicesController(
        IAppDbContext db,
        EmailConversationService conversations,
        ChronicServiceService chronic,
        SlaRuleService slaRules,
        IObjectStorage storage)
    {
        _db = db;
        _conversations = conversations;
        _chronic = chronic;
        _slaRules = slaRules;
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(
        [FromQuery] AuditStatus? status,
        [FromQuery] AuditQueue? queue,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && !PermissionService.CanBill(role))
            return Forbid();

        var query = _db.AuditServices.AsQueryable();

        if (PermissionService.CanBill(role) && !PermissionService.SeesAllQueues(role) && role == UserRole.Facturacion)
            query = query.Where(s =>
                s.Status == AuditStatus.Verde ||
                s.Status == AuditStatus.Celeste);

        if (!PermissionService.SeesAllQueues(role))
        {
            if (role == UserRole.Telemedicina)
                query = query.Where(s => s.Queue == AuditQueue.Telemedicina);
            else if (role == UserRole.Cronicos)
                query = query.Where(s => s.Queue == AuditQueue.Cronicos);
            else if (role == UserRole.Operador)
            {
                var userId = GetUserId();
                query = query.Where(s => s.Queue == AuditQueue.General && s.OperadorId == userId);
            }
        }

        if (status.HasValue) query = query.Where(s => s.Status == status);
        if (queue.HasValue) query = query.Where(s => s.Queue == queue);

        var warnByKey = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.IsEnabled)
            .Select(r => new { r.Queue, r.Status, r.WarnBeforeHours })
            .ToListAsync(ct);
        var warnMap = warnByKey.ToDictionary(
            r => (r.Queue, r.Status),
            r => r.WarnBeforeHours);

        var items = await query
            .OrderByDescending(s => s.FechaIngresoUtc)
            .Select(s => new
            {
                s.Id,
                s.Numero,
                s.PacienteId,
                s.Paciente,
                s.Dni,
                s.Art,
                s.NumeroSiniestro,
                s.TelefonoPaciente,
                s.EmailPaciente,
                s.TipoServicio,
                s.Especialidad,
                s.Profesional,
                s.PrestadorId,
                s.OperadorId,
                OperadorName = s.Operador != null ? s.Operador.Name : null,
                s.Queue,
                s.Status,
                s.Urgency,
                s.FechaIngresoUtc,
                s.FechaTurnoUtc,
                s.FechaConsultaUtc,
                s.SlaDeadlineUtc,
                s.ValorPactado,
                s.ValorConciliadoArt,
                s.TipoProfesional,
                s.PorcentajeConciliacionEspecialista,
                s.PrecioCatalogoId,
                s.PresupuestoEnviado,
                s.AutorizacionArt,
                s.AutorizacionCodigo,
                s.AutorizacionDocumentoAttachmentId,
                s.Autofisica,
                s.Notas,
                s.IsChronicPeriodic,
                s.ChronicPeriodicity,
                s.ChronicIntervalDays,
                s.ChronicScheduleStartUtc,
                s.NextRenewalDueUtc,
                s.LastRenewedAtUtc,
                s.ChronicRenewalCount,
                s.NeedsOperadorAssignment
            })
            .ToListAsync(ct);

        var shaped = items.Select(s => new
        {
            s.Id,
            s.Numero,
            s.PacienteId,
            s.Paciente,
            s.Dni,
            s.Art,
            s.NumeroSiniestro,
            s.TelefonoPaciente,
            s.EmailPaciente,
            s.TipoServicio,
            s.Especialidad,
            s.Profesional,
            s.PrestadorId,
            s.OperadorId,
            s.OperadorName,
            s.Queue,
            s.Status,
            s.Urgency,
            s.FechaIngresoUtc,
            s.FechaTurnoUtc,
            s.FechaConsultaUtc,
            s.SlaDeadlineUtc,
            s.ValorPactado,
            s.ValorConciliadoArt,
            s.TipoProfesional,
            s.PorcentajeConciliacionEspecialista,
            s.PrecioCatalogoId,
            s.PresupuestoEnviado,
            s.AutorizacionArt,
            s.AutorizacionCodigo,
            s.AutorizacionDocumentoAttachmentId,
            s.Autofisica,
            s.Notas,
            s.IsChronicPeriodic,
            s.ChronicPeriodicity,
            s.ChronicIntervalDays,
            s.ChronicScheduleStartUtc,
            s.NextRenewalDueUtc,
            s.LastRenewedAtUtc,
            s.ChronicRenewalCount,
            s.NeedsOperadorAssignment,
            SlaWarnBeforeHours = warnMap.GetValueOrDefault((s.Queue, s.Status), 12)
        });

        return Ok(shaped);
    }

    [HttpPost("chronic")]
    public async Task<ActionResult<object>> CreateChronic(
        [FromBody] CreateChronicServiceRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            var service = await _chronic.CreateManualAsync(
                new CreateChronicServiceCommand(
                    request.Paciente,
                    request.Art,
                    request.NumeroSiniestro,
                    request.TelefonoPaciente,
                    request.EmailPaciente,
                    request.Periodicity,
                    request.IntervalDays,
                    request.ScheduleStartUtc,
                    request.OperadorId,
                    request.TipoServicio ?? ServiceType.Consultorio,
                    request.Especialidad,
                    request.Dni,
                    request.Notas),
                ct);

            var loaded = await _db.AuditServices
                .Include(s => s.Operador)
                .FirstAsync(s => s.Id == service.Id, ct);
            return Ok(MapService(loaded));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
    {
        var s = await _db.AuditServices
            .Include(x => x.Operador)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        return Ok(MapService(s));
    }

    [HttpGet("{id:guid}/correspondence")]
    public async Task<ActionResult<EmailRequestDetailDto>> Correspondence(Guid id, CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) &&
            !PermissionService.CanBill(role) &&
            role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var exists = await _db.AuditServices.AnyAsync(s => s.Id == id, ct);
        if (!exists) return NotFound();

        var detail = await _conversations.GetByServiceAsync(id, ct);
        return detail is null ? NotFound(new { error = "Sin conversación vinculada." }) : Ok(detail);
    }

    [HttpPost("{id:guid}/send-email")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<ActionResult<EmailMessageDto>> SendEmail(
        Guid id,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var exists = await _db.AuditServices.AnyAsync(s => s.Id == id, ct);
        if (!exists) return NotFound();

        try
        {
            if (!Request.HasFormContentType)
            {
                var json = await Request.ReadFromJsonAsync<SendEmailRequestDto>(ct)
                    ?? throw new InvalidOperationException("Cuerpo inválido.");
                var messageJson = await _conversations.SendNewEmailAsync(
                    id,
                    GetUserId(),
                    json with
                    {
                        IdempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault()
                            ?? json.IdempotencyKey
                    },
                    [],
                    ct);
                return Accepted(messageJson);
            }

            var form = await Request.ReadFormAsync(ct);
            var to = SplitAddresses(form["to"].ToString());
            var cc = SplitAddresses(form["cc"].ToString());
            var subject = form["subject"].ToString();
            var bodyText = form["bodyText"].ToString();
            var bodyHtml = form["bodyHtml"].ToString();
            var idempotency = Request.Headers["Idempotency-Key"].FirstOrDefault()
                ?? form["idempotencyKey"].ToString();

            var existingIds = form["existingAttachmentIds"]
                .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

            var uploads = new List<UploadedEmailFile>();
            foreach (var file in form.Files)
            {
                if (file.Length <= 0) continue;
                uploads.Add(new UploadedEmailFile(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    file.OpenReadStream()));
            }

            var message = await _conversations.SendNewEmailAsync(
                id,
                GetUserId(),
                new SendEmailRequestDto(
                    to,
                    cc.Count > 0 ? cc : null,
                    subject,
                    bodyText,
                    string.IsNullOrWhiteSpace(bodyHtml) ? null : bodyHtml,
                    string.IsNullOrWhiteSpace(idempotency) ? null : idempotency,
                    existingIds),
                uploads,
                ct);
            return Accepted(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static List<string> SplitAddresses(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

    [HttpPost("{id:guid}/reply")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<ActionResult<EmailMessageDto>> Reply(
        Guid id,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var requestId = await _db.EmailRequests
            .Where(r => r.AuditServiceId == id)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct)
            ?? await _db.AuditServices
                .Where(s => s.Id == id)
                .Select(s => s.EmailRequestId)
                .FirstOrDefaultAsync(ct);

        if (requestId is null)
            return NotFound(new { error = "Sin conversación vinculada." });

        try
        {
            ReplyRequestDto body;
            var uploads = new List<UploadedEmailFile>();

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                var to = SplitAddresses(form["to"].ToString());
                var cc = SplitAddresses(form["cc"].ToString());
                var bodyText = form["bodyText"].ToString();
                var bodyHtml = form["bodyHtml"].ToString();
                var idempotency = Request.Headers["Idempotency-Key"].FirstOrDefault()
                    ?? form["idempotencyKey"].ToString();
                var existingIds = form["existingAttachmentIds"]
                    .Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();

                body = new ReplyRequestDto(
                    to,
                    cc.Count > 0 ? cc : null,
                    bodyText,
                    string.IsNullOrWhiteSpace(bodyHtml) ? null : bodyHtml,
                    string.IsNullOrWhiteSpace(idempotency) ? null : idempotency,
                    existingIds);

                foreach (var file in form.Files)
                {
                    if (file.Length <= 0) continue;
                    uploads.Add(new UploadedEmailFile(
                        file.FileName,
                        file.ContentType,
                        file.Length,
                        file.OpenReadStream()));
                }
            }
            else
            {
                body = await Request.ReadFromJsonAsync<ReplyRequestDto>(ct)
                    ?? throw new InvalidOperationException("Cuerpo inválido.");
                var idempotency = Request.Headers["Idempotency-Key"].FirstOrDefault()
                    ?? body.IdempotencyKey;
                body = body with { IdempotencyKey = idempotency };
            }

            var message = await _conversations.ReplyAsync(
                requestId.Value,
                GetUserId(),
                body,
                uploads,
                ct);
            return Accepted(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/transition")]
    public async Task<IActionResult> Transition(
        Guid id,
        [FromBody] TransitionRequest request,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role)) return Forbid();

        var service = await _db.AuditServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();

        try
        {
            if (request.NextStatus == AuditStatus.Amarillo && request.FechaTurnoUtc.HasValue)
            {
                if (request.PrestadorId is Guid prestadorId)
                {
                    var prestador = await _db.Prestadores
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == prestadorId && p.IsActive, ct);
                    if (prestador is null)
                        return BadRequest(new { error = "Prestador no encontrado o inactivo." });
                    service.SetTurno(
                        request.FechaTurnoUtc.Value,
                        string.IsNullOrWhiteSpace(request.Profesional) ? prestador.Nombre : request.Profesional,
                        prestador.Id,
                        prestador.ValorConsulta,
                        prestador.RequierePagoAnticipado);
                }
                else
                {
                    service.SetTurno(request.FechaTurnoUtc.Value, request.Profesional);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AutorizacionCodigo)
                || request.AutorizacionDocumentoAttachmentId.HasValue)
            {
                var authError = await ValidateAutorizacionAttachmentAsync(
                    id,
                    request.AutorizacionDocumentoAttachmentId,
                    ct);
                if (authError is not null)
                    return BadRequest(new { error = authError });

                service.SetAutorizacion(
                    request.AutorizacionCodigo,
                    request.AutorizacionDocumentoAttachmentId);
            }

            var anchor = request.NextStatus == AuditStatus.Azul
                ? (service.FechaConsultaUtc ?? DateTime.UtcNow)
                : DateTime.UtcNow;
            var deadline = await _slaRules.ComputeDeadlineAsync(
                service.Queue,
                request.NextStatus,
                anchor,
                ct);

            service.TransitionTo(request.NextStatus, GetUserId(), request.Reason, deadline);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/flags")]
    public async Task<IActionResult> UpdateFlags(
        Guid id,
        [FromBody] UpdateFlagsRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole())) return Forbid();

        var service = await _db.AuditServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();

        service.UpdateCommercialFlags(
            request.PresupuestoEnviado,
            request.AutorizacionArt,
            request.Autofisica,
            request.ValorPactado,
            request.ValorConciliadoArt);

        if (request.Notas is not null)
            service.UpdateNotes(request.Notas);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/precios")]
    public async Task<IActionResult> SetPrecios(
        Guid id,
        [FromBody] SetPreciosRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole())) return Forbid();

        var service = await _db.AuditServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();

        try
        {
            decimal? valorConciliado = request.ValorConciliadoArt;
            Guid? catalogoId = request.PrecioCatalogoId;
            int? pct = request.PorcentajeConciliacionEspecialista;

            if (request.TipoProfesional == TipoProfesional.Especialista)
            {
                pct ??= 100;
                if (pct is not (50 or 100))
                    return BadRequest(new { error = "Para especialista elegí 50% más o 100% más del precio." });
            }
            else
            {
                pct = null;
            }

            if (request.PrecioCatalogoId is Guid precioId)
            {
                var precio = await _db.PreciosCatalogo
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == precioId && p.IsActive, ct);
                if (precio is null)
                    return BadRequest(new { error = "Precio de catálogo no encontrado o inactivo." });
                if (precio.TipoProfesional != request.TipoProfesional)
                    return BadRequest(new { error = "El precio elegido no coincide con auditor/especialista." });
                catalogoId = precio.Id;
                if (!valorConciliado.HasValue)
                {
                    valorConciliado = request.TipoProfesional == TipoProfesional.Especialista && pct.HasValue
                        ? Math.Round(precio.Valor * (1m + pct.Value / 100m), 2, MidpointRounding.AwayFromZero)
                        : precio.Valor;
                }
            }

            service.SetPreciosRojo(
                request.TipoProfesional,
                request.ValorConsulta,
                valorConciliado,
                catalogoId,
                pct);

            await _db.SaveChangesAsync(ct);
            return Ok(MapService(service));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/chronic-schedule")]
    public async Task<IActionResult> UpdateChronicSchedule(
        Guid id,
        [FromBody] UpdateChronicScheduleRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            await _chronic.UpdateScheduleAsync(
                id,
                new UpdateChronicScheduleCommand(
                    request.Periodicity,
                    request.IntervalDays,
                    request.ScheduleStartUtc),
                ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IEnumerable<object>>> ListAttachments(Guid id, CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && !PermissionService.CanBill(role)
            && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var exists = await _db.AuditServices.AnyAsync(s => s.Id == id, ct);
        if (!exists) return NotFound();

        var items = await _db.ServiceAttachments
            .AsNoTracking()
            .Where(a => a.AuditServiceId == id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadAttachments(Guid id, CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var exists = await _db.AuditServices.AnyAsync(s => s.Id == id, ct);
        if (!exists) return NotFound();

        if (!Request.HasFormContentType)
            return BadRequest(new { error = "Se esperaba multipart/form-data con archivos." });

        var form = await Request.ReadFormAsync(ct);
        var files = form.Files;
        if (files.Count == 0)
            return BadRequest(new { error = "Seleccioná al menos un archivo." });

        const long maxBytes = 50L * 1024 * 1024;
        var created = new List<object>();

        foreach (var file in files)
        {
            if (file.Length <= 0)
                return BadRequest(new { error = $"El archivo '{file.FileName}' está vacío." });
            if (file.Length > maxBytes)
                return BadRequest(new { error = $"'{file.FileName}' supera el máximo de 50 MB." });
            if (!IsAllowedServiceAttachment(file.FileName, file.ContentType))
                return BadRequest(new { error = $"Tipo no permitido: '{file.FileName}'. Usá PDF, Word, Excel o imagen." });

            await using var stream = file.OpenReadStream();
            var key = await _storage.UploadAsync(
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                $"services/{id}",
                ct);

            var entity = ServiceAttachment.Create(
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length,
                key,
                s3Bucket: null,
                auditServiceId: id);

            _db.Add(entity);
            created.Add(new
            {
                entity.Id,
                entity.FileName,
                entity.ContentType,
                entity.SizeBytes,
                entity.CreatedAtUtc
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(created);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        var attachment = await _db.ServiceAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AuditServiceId == id, ct);
        if (attachment is null)
            return NotFound(new { error = "Adjunto no encontrado en este caso." });

        // Quita la relación con el caso; el archivo puede seguir en el historial de correo.
        attachment.UnlinkFromService();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool IsAllowedServiceAttachment(string fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };
        if (allowedExt.Contains(ext))
            return true;

        var ct = (contentType ?? string.Empty).ToLowerInvariant();
        return ct.StartsWith("image/") ||
               ct is "application/pdf"
                   or "application/msword"
                   or "application/vnd.ms-excel"
                   or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                   or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    [HttpPost("{id:guid}/assign-operador")]
    public async Task<IActionResult> AssignOperador(
        Guid id,
        [FromBody] AssignOperadorRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            await _chronic.AssignOperadorAsync(id, request.OperadorId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/autorizacion")]
    public async Task<IActionResult> SetAutorizacion(
        Guid id,
        [FromBody] SetAutorizacionRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole())) return Forbid();

        var service = await _db.AuditServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();

        try
        {
            var authError = await ValidateAutorizacionAttachmentAsync(
                id,
                request.AutorizacionDocumentoAttachmentId,
                ct);
            if (authError is not null)
                return BadRequest(new { error = authError });

            service.SetAutorizacion(request.AutorizacionCodigo, request.AutorizacionDocumentoAttachmentId);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/coordinacion-tardia")]
    public async Task<IActionResult> MarkCoordinacionTardia(
        Guid id,
        [FromBody] MarkTardiaRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole())) return Forbid();

        var service = await _db.AuditServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();

        try
        {
            service.ApplyChronicSchedule(
                request.Periodicity ?? ChronicPeriodicity.Monthly,
                request.IntervalDays,
                request.ScheduleStartUtc ?? DateTime.UtcNow);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/renew")]
    public async Task<IActionResult> RenewChronic(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            await _chronic.RenewAsync(id, GetUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<string?> ValidateAutorizacionAttachmentAsync(
        Guid serviceId,
        Guid? attachmentId,
        CancellationToken ct)
    {
        if (attachmentId is not Guid id) return null;
        var attachment = await _db.ServiceAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.AuditServiceId == serviceId, ct);
        if (attachment is null)
            return "El PDF de autorización no pertenece a este caso.";
        var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();
        if (ext != ".pdf"
            && !string.Equals(attachment.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return "La autorización documental debe ser un PDF.";
        return null;
    }

    private static object MapService(Domain.Entities.AuditService s) => new
    {
        s.Id,
        s.Numero,
        s.PacienteId,
        s.Paciente,
        s.Dni,
        s.Art,
        s.NumeroSiniestro,
        s.TelefonoPaciente,
        s.EmailPaciente,
        s.TipoServicio,
        s.Especialidad,
        s.Profesional,
        s.PrestadorId,
        s.OperadorId,
        OperadorName = s.Operador?.Name,
        s.Queue,
        s.Status,
        s.Urgency,
        s.FechaIngresoUtc,
        s.FechaTurnoUtc,
        s.FechaConsultaUtc,
        s.SlaDeadlineUtc,
        s.ValorPactado,
        s.ValorConciliadoArt,
        s.TipoProfesional,
        s.PorcentajeConciliacionEspecialista,
        s.PrecioCatalogoId,
        s.RequierePagoAnticipado,
        s.PresupuestoEnviado,
        s.AutorizacionArt,
        s.AutorizacionCodigo,
        s.AutorizacionDocumentoAttachmentId,
        s.Autofisica,
        s.Notas,
        s.IsChronicPeriodic,
        s.ChronicPeriodicity,
        s.ChronicIntervalDays,
        s.ChronicScheduleStartUtc,
        s.NextRenewalDueUtc,
        s.LastRenewedAtUtc,
        s.ChronicRenewalCount,
        s.NeedsOperadorAssignment
    };

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record TransitionRequest(
    AuditStatus NextStatus,
    DateTime? FechaTurnoUtc,
    string? Profesional,
    Guid? PrestadorId,
    string? Reason,
    string? AutorizacionCodigo = null,
    Guid? AutorizacionDocumentoAttachmentId = null);

public sealed record SetAutorizacionRequest(
    string? AutorizacionCodigo,
    Guid? AutorizacionDocumentoAttachmentId);

public sealed record MarkTardiaRequest(
    ChronicPeriodicity? Periodicity,
    int? IntervalDays,
    DateTime? ScheduleStartUtc);

public sealed record UpdateFlagsRequest(
    bool? PresupuestoEnviado,
    bool? AutorizacionArt,
    bool? Autofisica,
    decimal? ValorPactado,
    string? Notas,
    decimal? ValorConciliadoArt = null);

public sealed record SetPreciosRequest(
    TipoProfesional TipoProfesional,
    decimal? ValorConsulta,
    decimal? ValorConciliadoArt,
    Guid? PrecioCatalogoId,
    int? PorcentajeConciliacionEspecialista = null);

public sealed record CreateChronicServiceRequest(
    string Paciente,
    string Art,
    string NumeroSiniestro,
    string TelefonoPaciente,
    string EmailPaciente,
    ChronicPeriodicity Periodicity,
    int? IntervalDays,
    DateTime? ScheduleStartUtc,
    Guid? OperadorId,
    ServiceType? TipoServicio,
    string? Especialidad,
    string? Dni,
    string? Notas);

public sealed record UpdateChronicScheduleRequest(
    ChronicPeriodicity Periodicity,
    int? IntervalDays,
    DateTime? ScheduleStartUtc);

public sealed record AssignOperadorRequest(Guid OperadorId);
