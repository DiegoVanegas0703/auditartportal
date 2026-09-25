using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class GmailSyncState : Entity
{
    public string Mailbox { get; private set; } = string.Empty;
    public string? LastHistoryId { get; private set; }
    public DateTime? LastSuccessfulSyncAtUtc { get; private set; }
    public string? LastError { get; private set; }

    private GmailSyncState() { }

    public static GmailSyncState Create(string mailbox) =>
        new() { Mailbox = mailbox.Trim().ToLowerInvariant() };

    public void MarkSuccess(string? historyId)
    {
        if (!string.IsNullOrWhiteSpace(historyId))
            LastHistoryId = historyId;
        LastSuccessfulSyncAtUtc = DateTime.UtcNow;
        LastError = null;
        Touch();
    }

    public void MarkError(string error)
    {
        LastError = error.Length > 2000 ? error[..2000] : error;
        Touch();
    }

    public void ClearHistory()
    {
        LastHistoryId = null;
        Touch();
    }
}
