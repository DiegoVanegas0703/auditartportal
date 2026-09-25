using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class EmailMessage : Entity
{
    public Guid EmailThreadId { get; private set; }
    public EmailThread? EmailThread { get; private set; }

    public string? ProviderMessageId { get; private set; }
    public string? InternetMessageId { get; private set; }
    public EmailDirection Direction { get; private set; }
    public EmailMessageStatus Status { get; private set; }

    public string FromAddress { get; private set; } = string.Empty;
    public List<string> ToAddresses { get; private set; } = [];
    public List<string> CcAddresses { get; private set; } = [];
    public string? ReplyTo { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string BodyText { get; private set; } = string.Empty;
    public string? BodyHtml { get; private set; }
    public string? InReplyTo { get; private set; }
    public string? ReferencesHeader { get; private set; }

    public DateTime OccurredAtUtc { get; private set; } = DateTime.UtcNow;
    public Guid? SentByUserId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? LastError { get; private set; }
    public int AttemptCount { get; private set; }

    /// <summary>Puente temporal hacia IncomingEmail durante la transición.</summary>
    public Guid? IncomingEmailId { get; private set; }

    public ICollection<EmailAttachment> Attachments { get; private set; } = new List<EmailAttachment>();
    public EmailSendOutbox? OutboxItem { get; private set; }

    private EmailMessage() { }

    public static EmailMessage CreateInbound(
        Guid emailThreadId,
        string providerMessageId,
        string fromAddress,
        string subject,
        string bodyText,
        DateTime occurredAtUtc,
        IEnumerable<string>? to = null,
        IEnumerable<string>? cc = null,
        string? bodyHtml = null,
        string? internetMessageId = null,
        string? inReplyTo = null,
        string? references = null,
        string? replyTo = null,
        Guid? incomingEmailId = null)
    {
        return new EmailMessage
        {
            EmailThreadId = emailThreadId,
            ProviderMessageId = providerMessageId,
            InternetMessageId = internetMessageId,
            Direction = EmailDirection.Inbound,
            Status = EmailMessageStatus.Received,
            FromAddress = fromAddress.Trim(),
            ToAddresses = NormalizeAddresses(to),
            CcAddresses = NormalizeAddresses(cc),
            ReplyTo = replyTo?.Trim(),
            Subject = string.IsNullOrWhiteSpace(subject) ? "(Sin asunto)" : subject.Trim(),
            BodyText = bodyText ?? string.Empty,
            BodyHtml = bodyHtml,
            InReplyTo = inReplyTo,
            ReferencesHeader = references,
            OccurredAtUtc = occurredAtUtc,
            IncomingEmailId = incomingEmailId
        };
    }

    public static EmailMessage CreateOutboundPending(
        Guid emailThreadId,
        string fromAddress,
        IEnumerable<string> to,
        IEnumerable<string>? cc,
        string subject,
        string bodyText,
        string? bodyHtml,
        string? inReplyTo,
        string? references,
        Guid sentByUserId,
        string idempotencyKey)
    {
        return new EmailMessage
        {
            EmailThreadId = emailThreadId,
            Direction = EmailDirection.Outbound,
            Status = EmailMessageStatus.PendingSend,
            FromAddress = fromAddress.Trim(),
            ToAddresses = NormalizeAddresses(to),
            CcAddresses = NormalizeAddresses(cc),
            Subject = string.IsNullOrWhiteSpace(subject) ? "(Sin asunto)" : subject.Trim(),
            BodyText = bodyText ?? string.Empty,
            BodyHtml = bodyHtml,
            InReplyTo = inReplyTo,
            ReferencesHeader = references,
            OccurredAtUtc = DateTime.UtcNow,
            SentByUserId = sentByUserId,
            IdempotencyKey = idempotencyKey
        };
    }

    public void MarkSending()
    {
        Status = EmailMessageStatus.Sending;
        AttemptCount++;
        Touch();
    }

    public void MarkSent(string providerMessageId, string? internetMessageId)
    {
        ProviderMessageId = providerMessageId;
        if (!string.IsNullOrWhiteSpace(internetMessageId))
            InternetMessageId = internetMessageId;
        Status = EmailMessageStatus.Sent;
        LastError = null;
        Touch();
    }

    public void MarkFailed(string error)
    {
        Status = EmailMessageStatus.Failed;
        LastError = error.Length > 2000 ? error[..2000] : error;
        Touch();
    }

    private static List<string> NormalizeAddresses(IEnumerable<string>? addresses) =>
        (addresses ?? [])
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
}
