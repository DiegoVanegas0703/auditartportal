using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

/// <summary>
/// Hilo de Gmail (ThreadId). Pertenece a un EmailRequest.
/// </summary>
public class EmailThread : Entity
{
    public Guid EmailRequestId { get; private set; }
    public EmailRequest? EmailRequest { get; private set; }

    public string ProviderThreadId { get; private set; } = string.Empty;
    public string Mailbox { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<EmailMessage> Messages { get; private set; } = new List<EmailMessage>();

    private EmailThread() { }

    public static EmailThread Create(
        Guid emailRequestId,
        string mailbox,
        string providerThreadId,
        string subject,
        DateTime lastMessageAtUtc)
    {
        return new EmailThread
        {
            EmailRequestId = emailRequestId,
            Mailbox = mailbox.Trim().ToLowerInvariant(),
            ProviderThreadId = providerThreadId,
            Subject = string.IsNullOrWhiteSpace(subject) ? "(Sin asunto)" : subject.Trim(),
            LastMessageAtUtc = lastMessageAtUtc
        };
    }

    public void MoveToRequest(EmailRequest request)
    {
        EmailRequestId = request.Id;
        EmailRequest = request;
        Touch();
    }

    public void TouchLastMessage(DateTime atUtc, string? subject = null)
    {
        if (atUtc > LastMessageAtUtc)
            LastMessageAtUtc = atUtc;
        if (!string.IsNullOrWhiteSpace(subject))
            Subject = subject.Trim();
        Touch();
    }

    public void BindProviderThreadId(string providerThreadId)
    {
        if (string.IsNullOrWhiteSpace(providerThreadId))
            return;
        ProviderThreadId = providerThreadId.Trim();
        Touch();
    }
}
