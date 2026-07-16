using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IAppDbContext _db;

    public UsersController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(CancellationToken ct)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value;

        if (role is not (nameof(UserRole.Admin) or nameof(UserRole.Jefatura)))
            return Forbid();

        var users = await _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.DefaultQueue
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpGet("operators")]
    public async Task<ActionResult<IEnumerable<object>>> Operators(
        [FromQuery] AuditQueue? queue,
        CancellationToken ct)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value;

        if (role is not (nameof(UserRole.Admin) or nameof(UserRole.Jefatura)))
            return Forbid();

        var query = _db.Users.Where(u => u.IsActive);

        query = queue switch
        {
            AuditQueue.Telemedicina => query.Where(u => u.Role == UserRole.Telemedicina),
            AuditQueue.Cronicos => query.Where(u => u.Role == UserRole.Cronicos),
            AuditQueue.General => query.Where(u => u.Role == UserRole.Operador),
            _ => query.Where(u =>
                u.Role == UserRole.Operador
                || u.Role == UserRole.Telemedicina
                || u.Role == UserRole.Cronicos)
        };

        var users = await query
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.DefaultQueue })
            .ToListAsync(ct);

        return Ok(users);
    }
}
