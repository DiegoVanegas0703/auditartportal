using System.Security.Claims;
using Auditart.Application.Auth;
using Auditart.Application.Sla;
using Auditart.Domain.Enums;
using Auditart.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sla-rules")]
public class SlaRulesController : ControllerBase
{
    private readonly SlaRuleService _rules;

    public SlaRulesController(SlaRuleService rules)
    {
        _rules = rules;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SlaRuleDto>>> List(CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()))
            return Forbid();

        return Ok(await _rules.ListAsync(ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaRuleRequest body, CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()))
            return Forbid();

        try
        {
            await _rules.UpdateAsync(
                id,
                new UpdateSlaRuleCommand(
                    body.DurationValue,
                    body.DurationUnit,
                    body.WarnBeforeHours,
                    body.IsEnabled),
                ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("backfill")]
    public async Task<ActionResult<object>> Backfill(CancellationToken ct)
    {
        if (!PermissionService.CanViewReports(GetRole()))
            return Forbid();

        var updated = await _rules.BackfillMissingDeadlinesAsync(ct);
        return Ok(new { updated });
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.Parse<UserRole>(role!);
    }
}

public sealed record UpdateSlaRuleRequest(
    int DurationValue,
    SlaDurationUnit DurationUnit,
    int WarnBeforeHours,
    bool IsEnabled);
