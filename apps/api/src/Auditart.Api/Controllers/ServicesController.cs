using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
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

    public ServicesController(IAppDbContext db)
    {
        _db = db;
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
            query = query.Where(s => s.Status == AuditStatus.Verde);

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

        var items = await query
            .OrderByDescending(s => s.FechaIngresoUtc)
            .Select(s => new
            {
                s.Id,
                s.Numero,
                s.Paciente,
                s.Dni,
                s.Art,
                s.TipoServicio,
                s.Especialidad,
                s.Profesional,
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
                s.PresupuestoEnviado,
                s.AutorizacionArt,
                s.Autofisica,
                s.Notas
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
    {
        var s = await _db.AuditServices
            .Include(x => x.Operador)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        return Ok(s);
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
                service.SetTurno(request.FechaTurnoUtc.Value, request.Profesional);

            service.TransitionTo(request.NextStatus, GetUserId(), request.Reason);
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
            request.ValorPactado);

        if (request.Notas is not null)
            service.UpdateNotes(request.Notas);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

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
    string? Reason);

public sealed record UpdateFlagsRequest(
    bool? PresupuestoEnviado,
    bool? AutorizacionArt,
    bool? Autofisica,
    decimal? ValorPactado,
    string? Notas);
