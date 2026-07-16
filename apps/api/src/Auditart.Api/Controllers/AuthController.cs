using Auditart.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Login con Google ID token. En Development también acepta: idToken = "dev:email@dominio.com"
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokensDto>> GoogleLogin(
        [FromBody] GoogleLoginRequest request,
        CancellationToken ct)
    {
        try
        {
            var tokens = await _auth.LoginWithGoogleAsync(request.IdToken, ct);
            return Ok(tokens);
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
        [FromServices] Application.Abstractions.IAppDbContext db,
        CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(db.Users, u => u.Id == userId, ct);

        if (user is null) return Unauthorized();
        return Ok(AuthService.MapUser(user));
    }
}
