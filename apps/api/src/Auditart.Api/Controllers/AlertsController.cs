using System.Security.Claims;
using Auditart.Application.Sla;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly InAppAlertService _alerts;

    public AlertsController(InAppAlertService alerts)
    {
        _alerts = alerts;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InAppAlertDto>>> List(
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var items = await _alerts.ListForUserAsync(GetUserId(), unreadOnly, ct);
        return Ok(items);
    }

    [HttpGet("count")]
    public async Task<ActionResult<object>> Count(CancellationToken ct)
    {
        var count = await _alerts.CountUnreadAsync(GetUserId(), ct);
        return Ok(new { unread = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            await _alerts.MarkReadAsync(id, GetUserId(), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _alerts.MarkAllReadAsync(GetUserId(), ct);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
