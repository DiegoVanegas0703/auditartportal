using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.WhatsApp;
using Auditart.Domain.Enums;
using Auditart.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Auditart.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public class WhatsAppController : ControllerBase
{
    private readonly WhatsAppNotifyService _notify;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(
        WhatsAppNotifyService notify,
        IOptions<WhatsAppOptions> options,
        ILogger<WhatsAppController> logger)
    {
        _notify = notify;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    [Authorize]
    public ActionResult<object> Status() => Ok(new { enabled = _notify.IsEnabled });

    [HttpGet("services/{serviceId:guid}/turno-preview")]
    [Authorize]
    public async Task<ActionResult<WhatsAppTurnoPreviewDto>> PreviewTurno(Guid serviceId, CancellationToken ct)
    {
        if (!CanOperate()) return Forbid();
        try
        {
            return Ok(await _notify.PreviewTurnoAsync(serviceId, GetUserId(), ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("services/{serviceId:guid}/notify-turno")]
    [Authorize]
    public async Task<ActionResult<object>> NotifyTurno(Guid serviceId, CancellationToken ct)
    {
        if (!CanOperate()) return Forbid();
        try
        {
            var result = await _notify.NotifyTurnoAsync(serviceId, GetUserId(), ct);
            if (!result.Success)
                return BadRequest(new { error = result.Error });
            return Ok(new { phone = result.Phone, logId = result.LogId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Verificación del webhook de Meta (hub.challenge).</summary>
    [HttpGet("webhook")]
    [AllowAnonymous]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe"
            && !string.IsNullOrWhiteSpace(_options.WebhookVerifyToken)
            && token == _options.WebhookVerifyToken)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        _logger.LogInformation("WhatsApp webhook recibido ({Len} chars)", body.Length);
        return Ok();
    }

    private bool CanOperate()
    {
        var role = GetRole();
        return PermissionService.CanOperateBoard(role) || role is UserRole.Admin or UserRole.Jefatura;
    }

    private UserRole GetRole()
    {
        var raw = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.TryParse<UserRole>(raw, true, out var role) ? role : UserRole.Operador;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
