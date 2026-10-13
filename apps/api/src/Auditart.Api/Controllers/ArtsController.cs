using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/arts")]
public class ArtsController : ControllerBase
{
    private readonly IAppDbContext _db;

    public ArtsController(IAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArtDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var query = _db.Aseguradoras.AsNoTracking();
        if (!includeInactive)
            query = query.Where(a => a.IsActive);

        return Ok(await query
            .OrderBy(a => a.Nombre)
            .Select(a => new ArtDto(a.Id, a.Nombre, a.Cuit, a.IsActive))
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<ArtDto>> Create([FromBody] ArtUpsertBody body, CancellationToken ct)
    {
        if (!CanManage()) return Forbid();
        try
        {
            if (await NameTakenAsync(body.Nombre, null, ct))
                return Conflict(new { error = "Ya existe una ART con ese nombre." });

            var entity = Aseguradora.Create(body.Nombre, body.Cuit);
            _db.Add(entity);
            await _db.SaveChangesAsync(ct);
            return Ok(Map(entity));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ArtDto>> Update(Guid id, [FromBody] ArtUpsertBody body, CancellationToken ct)
    {
        if (!CanManage()) return Forbid();
        var entity = await _db.Aseguradoras.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return NotFound();
        try
        {
            if (await NameTakenAsync(body.Nombre, id, ct))
                return Conflict(new { error = "Ya existe una ART con ese nombre." });

            entity.Update(body.Nombre, body.Cuit, body.IsActive ?? entity.IsActive);
            await _db.SaveChangesAsync(ct);
            return Ok(Map(entity));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<bool> NameTakenAsync(string? nombre, Guid? exceptId, CancellationToken ct)
    {
        var normalized = (nombre ?? string.Empty).Trim().ToUpper();
        return await _db.Aseguradoras.AnyAsync(
            a => a.Nombre.ToUpper() == normalized && (exceptId == null || a.Id != exceptId),
            ct);
    }

    private static ArtDto Map(Aseguradora a) => new(a.Id, a.Nombre, a.Cuit, a.IsActive);

    private bool CanManage()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.TryParse<UserRole>(role, out var parsed) && parsed is UserRole.Admin or UserRole.Jefatura;
    }
}

public sealed record ArtDto(Guid Id, string Nombre, string Cuit, bool IsActive);

public sealed record ArtUpsertBody(string Nombre, string Cuit, bool? IsActive = null);
