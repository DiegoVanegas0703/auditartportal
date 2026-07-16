using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;
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

    public TriageController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpGet("emails")]
    public async Task<ActionResult<IEnumerable<IncomingEmailDto>>> ListPending(CancellationToken ct)
    {
        if (!IsJefaturaOrAdmin()) return Forbid();

        var items = await _db.IncomingEmails
            .Where(e => !e.IsAssigned)
            .OrderByDescending(e => e.ReceivedAtUtc)
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
                e.IsAssigned))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// La jefa elige cola y operador. Siempre crea servicio en estado Rojo.
    /// </summary>
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

        var operador = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.OperadorId && u.IsActive, ct);
        if (operador is null) return BadRequest(new { error = "Operador inválido." });

        var nextNumero = await _db.AuditServices.AnyAsync(ct)
            ? await _db.AuditServices.MaxAsync(s => s.Numero, ct) + 1
            : 1001;

        var urgency = email.Subject.Contains("URGENT", StringComparison.OrdinalIgnoreCase)
            ? UrgencyLevel.Critica
            : UrgencyLevel.Alta;

        var service = AuditService.CreateFromTriage(
            nextNumero,
            email.SuggestedPatientName ?? "Sin identificar",
            email.SuggestedArt ?? "Por definir",
            email.SuggestedServiceType ?? ServiceType.Consultorio,
            request.Queue,
            operador.Id,
            urgency,
            email.Id,
            notas: $"Derivado desde email: {email.Subject}");

        _db.Add(service);
        email.MarkAssigned(GetUserId(), service.Id);
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

    private static AuditServiceDto ToDto(AuditService s, string? operadorName) => new(
        s.Id, s.Numero, s.Paciente, s.Dni, s.Art, s.TipoServicio, s.Especialidad,
        s.Profesional, s.OperadorId, operadorName, s.Queue, s.Status, s.Urgency,
        s.FechaIngresoUtc, s.FechaTurnoUtc, s.FechaConsultaUtc, s.SlaDeadlineUtc,
        s.ValorPactado, s.PresupuestoEnviado, s.AutorizacionArt, s.Autofisica, s.Notas);
}

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
    bool IsAssigned);

public sealed record AssignEmailRequest(AuditQueue Queue, Guid OperadorId);

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
