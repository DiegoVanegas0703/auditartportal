using Auditart.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Auditart.Infrastructure.Gmail;

/// <summary>
/// Stub de Gmail. La integración real con dvelopmentcode@gmail.com se conecta aquí
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
        _logger.LogInformation("StubGmail: FetchUnread (sin integración aún). Cuenta objetivo: dvelopmentcode@gmail.com");
        return Task.FromResult<IReadOnlyList<GmailMessageDto>>(Array.Empty<GmailMessageDto>());
    }

    public Task<GmailMessageDto?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubGmail: GetMessage {MessageId}", messageId);
        return Task.FromResult<GmailMessageDto?>(null);
    }

    public Task<string?> GetProfileHistoryIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<GmailHistoryResult> FetchHistoryAddedAsync(
        string startHistoryId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubGmail: FetchHistoryAdded from {HistoryId}", startHistoryId);
        return Task.FromResult(new GmailHistoryResult([], null, HistoryExpired: false));
    }

    public Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubGmail: MarkAsRead {MessageId}", messageId);
        return Task.CompletedTask;
    }

    public Task ModifyThreadLabelsByNameAsync(
        string threadId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "StubGmail: ModifyThreadLabels thread={ThreadId} add=[{Add}] remove=[{Remove}]",
            threadId,
            string.Join(",", addLabelNames),
            string.Join(",", removeLabelNames));
        return Task.CompletedTask;
    }

    public Task ModifyMessageLabelsByNameAsync(
        string messageId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "StubGmail: ModifyMessageLabels message={MessageId} add=[{Add}] remove=[{Remove}]",
            messageId,
            string.Join(",", addLabelNames),
            string.Join(",", removeLabelNames));
        return Task.CompletedTask;
    }

    public Task<GmailSendResult> SendReplyAsync(GmailSendRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "StubGmail: SendReply thread={ThreadId} to={To}",
            request.ThreadId,
            string.Join(",", request.To));
        return Task.FromResult(new GmailSendResult(
            $"stub-{Guid.NewGuid():N}",
            request.ThreadId,
            request.InternetMessageId));
    }
}
