using Auditart.Application.Ai;
using Auditart.Application.Auth;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly AiAssistantService _ai;

    public AiController(AiAssistantService ai) => _ai = ai;

    [HttpGet("status")]
    public ActionResult<object> Status() =>
        Ok(new { chatEnabled = _ai.IsChatEnabled });

    [HttpGet("assistants")]
    public async Task<ActionResult<IReadOnlyList<AiAssistantDto>>> List(CancellationToken ct)
    {
        if (!CanUseAi()) return Forbid();
        return Ok(await _ai.ListAsync(ct));
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AiChatResponse>> Chat([FromBody] AiChatBody body, CancellationToken ct)
    {
        if (!CanUseAi()) return Forbid();
        if (body.AssistantId == Guid.Empty)
            return BadRequest(new { error = "Indicá el asistente." });
        if (body.Messages is null || body.Messages.Count == 0)
            return BadRequest(new { error = "Enviá al menos un mensaje." });

        try
        {
            var response = await _ai.ChatAsync(
                new AiChatRequest(
                    body.AssistantId,
                    body.Messages.Select(m => new AiChatMessageDto(m.Role, m.Content)).ToList()),
                ct);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Asistente no encontrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private bool CanUseAi()
    {
        var raw = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var role = Enum.TryParse<UserRole>(raw, true, out var r) ? r : UserRole.Operador;
        return PermissionService.CanOperateBoard(role)
            || PermissionService.CanTriage(role)
            || role is UserRole.Admin or UserRole.Jefatura;
    }
}

public sealed record AiChatBody(Guid AssistantId, List<AiChatMessageItem>? Messages);
public sealed record AiChatMessageItem(string Role, string Content);
