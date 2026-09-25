using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Triage;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/triage/requests")]
public sealed class TriageRequestsController : ControllerBase
{
    private readonly EmailConversationService _conversations;
    private readonly GmailIngestionService _ingestion;
    private readonly DenunciaPdfParser _denunciaParser;
    private readonly IAppDbContext _db;

    public TriageRequestsController(
        EmailConversationService conversations,
        GmailIngestionService ingestion,
        DenunciaPdfParser denunciaParser,
        IAppDbContext db)
    {
        _conversations = conversations;
        _ingestion = ingestion;
        _denunciaParser = denunciaParser;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedRequestsResult>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] bool ignored = false,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        return Ok(await _conversations.ListAsync(page, pageSize, ignored, tag, GetRole(), ct));
    }

    [HttpGet("{requestId:guid}")]
    public async Task<ActionResult<EmailRequestDetailDto>> Get(Guid requestId, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        var detail = await _conversations.GetAsync(requestId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{requestId:guid}/parse-denuncia")]
    public async Task<ActionResult<DenunciaParsedDto>> ParseDenuncia(
        Guid requestId,
        [FromBody] ParseDenunciaBody? body,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        if (body?.AttachmentId is null || body.AttachmentId == Guid.Empty)
            return BadRequest(new { error = "Debe seleccionar el adjunto PDF a parsear." });
        try
        {
            return Ok(await _denunciaParser.ParseFromRequestAsync(requestId, body.AttachmentId.Value, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{requestId:guid}/ignore")]
    public async Task<IActionResult> Ignore(Guid requestId, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        try
        {
            await _conversations.IgnoreAsync(requestId, GetUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{requestId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid requestId, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        try
        {
            await _conversations.RestoreAsync(requestId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{requestId:guid}/tags")]
    public async Task<ActionResult<IReadOnlyList<string>>> UpdateTags(
        Guid requestId,
        [FromBody] UpdateEmailTagsRequest request,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        try
        {
            return Ok(await _conversations.UpdateTagsAsync(requestId, request.Tags ?? [], ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeRequestsBody body, CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        if (body.SourceRequestIds is null || body.SourceRequestIds.Count == 0)
            return BadRequest(new { error = "Seleccioná al menos un requerimiento para unir." });

        try
        {
            await _conversations.MergeAsync(body.TargetRequestId, body.SourceRequestIds, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{requestId:guid}/assign")]
    public async Task<ActionResult<object>> Assign(
        Guid requestId,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        AssignRequestBody request;
        IFormFile? attachment = null;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        try
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                request = AssignRequestJson.FromForm(form).ToBody();
                attachment = form.Files.GetFile("attachment");
            }
            else
            {
                var json = await Request.ReadFromJsonAsync<AssignRequestJson>(jsonOptions, ct)
                    ?? throw new InvalidOperationException("Cuerpo de solicitud inválido.");
                request = json.ToBody();
            }
        }
        catch (JsonException ex)
        {
            return BadRequest(new { error = $"Datos de derivación inválidos: {ex.Message}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(request.TriageNote))
            return BadRequest(new { error = "La nota de triage es obligatoria." });

        try
        {
            Stream? attachmentStream = null;
            if (attachment is { Length: > 0 })
                attachmentStream = attachment.OpenReadStream();

            var service = await _conversations.AssignAsync(
                requestId,
                request.OperadorId,
                request.Queue,
                GetUserId(),
                request.TriageNote,
                request.Paciente,
                request.Dni,
                request.Art,
                request.NumeroSiniestro,
                request.TelefonoPaciente,
                request.EmailPaciente,
                request.TipoServicio,
                request.Especialidad,
                request.Urgency,
                attachmentStream,
                attachment?.FileName,
                attachment?.ContentType,
                attachment?.Length,
                ct);

            var operadorName = await _db.Users
                .Where(u => u.Id == service.OperadorId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(ct);

            return Ok(new
            {
                service.Id,
                service.Numero,
                service.Paciente,
                service.Dni,
                service.Art,
                service.NumeroSiniestro,
                service.TelefonoPaciente,
                service.EmailPaciente,
                service.TipoServicio,
                service.Especialidad,
                service.Profesional,
                service.OperadorId,
                OperadorName = operadorName,
                service.Queue,
                service.Status,
                service.Urgency,
                service.FechaIngresoUtc,
                service.FechaTurnoUtc,
                service.FechaConsultaUtc,
                service.SlaDeadlineUtc,
                service.ValorPactado,
                service.PresupuestoEnviado,
                service.AutorizacionArt,
                service.Autofisica,
                service.Notas,
                service.EmailRequestId
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{requestId:guid}/reply")]
    public async Task<ActionResult<EmailMessageDto>> Reply(
        Guid requestId,
        [FromBody] ReplyRequestDto body,
        CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        try
        {
            var idempotency = Request.Headers["Idempotency-Key"].FirstOrDefault()
                ?? body.IdempotencyKey;
            var message = await _conversations.ReplyAsync(
                requestId,
                GetUserId(),
                body with { IdempotencyKey = idempotency },
                uploads: null,
                ct);
            return Accepted(message);
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

    [HttpPost("backfill")]
    public async Task<ActionResult<GmailSyncResult>> Backfill(CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();
        return Ok(await _ingestion.BackfillFromIncomingAsync(ct));
    }

    private bool IsJefaturaOrAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return role is nameof(UserRole.Admin) or nameof(UserRole.Jefatura);
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record MergeRequestsBody(Guid TargetRequestId, IReadOnlyList<Guid> SourceRequestIds);

public sealed record AssignRequestBody(
    AuditQueue Queue,
    Guid OperadorId,
    string TriageNote,
    string? Paciente = null,
    string? Dni = null,
    string? Art = null,
    string? NumeroSiniestro = null,
    string? TelefonoPaciente = null,
    string? EmailPaciente = null,
    ServiceType? TipoServicio = null,
    string? Especialidad = null,
    UrgencyLevel? Urgency = null);

public sealed record ParseDenunciaBody(Guid? AttachmentId);

public sealed class AssignRequestJson
{
    public string Queue { get; set; } = "";
    public Guid OperadorId { get; set; }
    public string TriageNote { get; set; } = "";
    public string? Paciente { get; set; }
    public string? Dni { get; set; }
    public string? Art { get; set; }
    public string? NumeroSiniestro { get; set; }
    public string? TelefonoPaciente { get; set; }
    public string? EmailPaciente { get; set; }
    public string? TipoServicio { get; set; }
    public string? Especialidad { get; set; }
    public string? Urgency { get; set; }

    public static AssignRequestJson FromForm(IFormCollection form) => new()
    {
        Queue = form["queue"]!,
        OperadorId = Guid.Parse(form["operadorId"]!),
        TriageNote = form["triageNote"]!,
        Paciente = form["paciente"],
        Dni = form["dni"],
        Art = form["art"],
        NumeroSiniestro = form["numeroSiniestro"],
        TelefonoPaciente = form["telefonoPaciente"],
        EmailPaciente = form["emailPaciente"],
        TipoServicio = form["tipoServicio"],
        Especialidad = form["especialidad"],
        Urgency = form["urgency"],
    };

    public AssignRequestBody ToBody() => new(
        ParseEnum<AuditQueue>(Queue, nameof(Queue)),
        OperadorId,
        TriageNote,
        Paciente,
        Dni,
        Art,
        NumeroSiniestro,
        TelefonoPaciente,
        EmailPaciente,
        string.IsNullOrWhiteSpace(TipoServicio) ? null : ParseEnum<ServiceType>(TipoServicio, nameof(TipoServicio)),
        Especialidad,
        string.IsNullOrWhiteSpace(Urgency) ? null : ParseEnum<UrgencyLevel>(Urgency, nameof(Urgency)));

    private static TEnum ParseEnum<TEnum>(string value, string fieldName)
        where TEnum : struct, Enum
    {
        var normalized = value.Trim().Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Valor inválido para {fieldName}: {value}");
    }
}
