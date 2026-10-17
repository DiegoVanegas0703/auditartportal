using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.WhatsApp;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Auditart.Infrastructure.WhatsApp;

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
    public ActionResult<object> Status()
    {
        return Ok(new { enabled = _notify.IsEnabled });
    }

    [HttpPost("services/{serviceId:guid}/notify")]
    [Authorize]
    public async Task<ActionResult<object>> NotifyService(Guid serviceId, CancellationToken ct)
    {
        var role = GetRole();
        if (!PermissionService.CanOperateBoard(role) && role is not (UserRole.Admin or UserRole.Jefatura))
            return Forbid();

        try
        {
            var result = await _notify.NotifyStatusAsync(serviceId, ct: ct);
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

    private UserRole GetRole()
    {
        var raw = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.TryParse<UserRole>(raw, true, out var role) ? role : UserRole.Operador;
    }
}
