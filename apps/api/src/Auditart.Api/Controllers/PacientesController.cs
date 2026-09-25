using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.Pacientes;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pacientes")]
public class PacientesController : ControllerBase
{
    private readonly PacienteService _service;

    public PacientesController(PacienteService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PacienteDto>> Get(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole()) && !PermissionService.CanTriage(GetRole()))
            return Forbid();
        try
        {
            return Ok(await _service.GetAsync(id, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PacienteDto>> Update(
        Guid id,
        [FromBody] UpdatePacienteRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole()) && !PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            return Ok(await _service.UpdateAsync(id, request, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or Domain.Exceptions.DomainException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/prestaciones")]
    public async Task<IActionResult> CreatePrestacion(
        Guid id,
        [FromBody] CreatePrestacionRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole()) && !PermissionService.CanTriage(GetRole()))
            return Forbid();

        try
        {
            var created = await _service.CreatePrestacionAsync(id, request, GetUserId(), ct);
            return Ok(new { created.Id, created.Numero });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill(CancellationToken ct)
    {
        if (GetRole() is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();
        await _service.BackfillMissingAsync(ct);
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
