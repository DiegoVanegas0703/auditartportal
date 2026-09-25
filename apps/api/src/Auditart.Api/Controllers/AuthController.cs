using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokensDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        try
        {
            var tokens = await _auth.LoginAsync(request.Email, request.Password, ct);
            return Ok(tokens);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        try
        {
            await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokensDto>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        try
        {
            var tokens = await _auth.RefreshAsync(request.RefreshToken, ct);
            return Ok(tokens);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _auth.RevokeRefreshAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> Me(
        [FromServices] IAppDbContext db,
        CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.Users, u => u.Id == userId, ct);

        if (user is null) return Unauthorized();
        return Ok(AuthService.MapUser(user));
    }
}
