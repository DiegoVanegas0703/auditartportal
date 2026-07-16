using Auditart.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Gmail;

/// <summary>
/// Stub de Gmail. La integración real con developmentcode@gmail.com se conecta aquí
/// sin cambiar contratos de Application/API.
/// </summary>
public class StubGmailInboxService : IGmailInboxService
{
    private readonly ILogger<StubGmailInboxService> _logger;

    public StubGmailInboxService(ILogger<StubGmailInboxService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<GmailMessageDto>> FetchUnreadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubGmail: FetchUnread (sin integración aún). Cuenta objetivo: developmentcode@gmail.com");
        return Task.FromResult<IReadOnlyList<GmailMessageDto>>(Array.Empty<GmailMessageDto>());
    }

    public Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubGmail: MarkAsRead {MessageId}", messageId);
        return Task.CompletedTask;
    }
}
