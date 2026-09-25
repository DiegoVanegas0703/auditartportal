using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Users;
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
    private readonly UserAdminService _admin;

    public UsersController(IAppDbContext db, UserAdminService admin)
    {
        _db = db;
        _admin = admin;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List(CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();

        var users = await _admin.ListAllAsync(ct);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            return Ok(await _admin.CreateUserAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserListItemDto>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            return Ok(await _admin.UpdateUserAsync(id, request, ct));
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

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            await _admin.DeactivateUserAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("operators")]
    public async Task<ActionResult<IEnumerable<object>>> Operators(
        [FromQuery] AuditQueue? queue,
        CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanTriage(role))
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

    private bool IsAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return role == nameof(UserRole.Admin);
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }
}
