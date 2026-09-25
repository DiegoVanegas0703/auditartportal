using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.Precios;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/precios")]
public class PreciosController : ControllerBase
{
    private readonly PrecioCatalogoService _service;

    public PreciosController(PrecioCatalogoService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<PrecioCatalogoListDto>> List(
        [FromQuery] TipoProfesional? tipo,
        [FromQuery] string? art,
        [FromQuery] string? q,
        [FromQuery] bool? soloActivos,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!PermissionService.CanManagePrecios(GetRole())) return Forbid();
        return Ok(await _service.ListAsync(tipo, art, q, soloActivos, page, pageSize, ct));
    }

    [HttpGet("options")]
    public async Task<ActionResult<IReadOnlyList<PrecioOptionDto>>> Options(
        [FromQuery] TipoProfesional tipo,
        [FromQuery] string? art,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        if (!PermissionService.CanOperateBoard(GetRole()) && !PermissionService.CanManagePrecios(GetRole()))
            return Forbid();
        return Ok(await _service.OptionsAsync(tipo, art, q, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PrecioCatalogoDto>> Get(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanManagePrecios(GetRole()) && !PermissionService.CanOperateBoard(GetRole()))
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

    [HttpPost]
    public async Task<ActionResult<PrecioCatalogoDto>> Create(
        [FromBody] UpsertPrecioRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanManagePrecios(GetRole())) return Forbid();
        try
        {
            var created = await _service.CreateAsync(request, ct);
            return Ok(created);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or Domain.Exceptions.DomainException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PrecioCatalogoDto>> Update(
        Guid id,
        [FromBody] UpsertPrecioRequest request,
        CancellationToken ct)
    {
        if (!PermissionService.CanManagePrecios(GetRole())) return Forbid();
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

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanManagePrecios(GetRole())) return Forbid();
        try
        {
            await _service.SetActiveAsync(id, true, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!PermissionService.CanManagePrecios(GetRole())) return Forbid();
        try
        {
            await _service.SetActiveAsync(id, false, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }
}
