using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class IncomingEmail : Entity
{
    public string GmailMessageId { get; private set; } = string.Empty;
    public string FromAddress { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; private set; }
    public int AttachmentCount { get; private set; }
    public bool IsAssigned { get; private set; }
    public AuditQueue? SuggestedQueue { get; private set; }
    public ServiceType? SuggestedServiceType { get; private set; }
    public string? SuggestedArt { get; private set; }
    public string? SuggestedPatientName { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public Guid? ResultingServiceId { get; private set; }

    public ICollection<ServiceAttachment> Attachments { get; private set; } = new List<ServiceAttachment>();

    private IncomingEmail() { }

    public static IncomingEmail Create(
        string gmailMessageId,
        string fromAddress,
        string subject,
        string body,
        DateTime receivedAtUtc,
        int attachmentCount,
        AuditQueue? suggestedQueue = null,
        ServiceType? suggestedServiceType = null,
        string? suggestedArt = null,
        string? suggestedPatientName = null)
    {
        return new IncomingEmail
        {
            GmailMessageId = gmailMessageId,
            FromAddress = fromAddress,
            Subject = subject,
            Body = body,
            ReceivedAtUtc = receivedAtUtc,
            AttachmentCount = attachmentCount,
            SuggestedQueue = suggestedQueue,
            SuggestedServiceType = suggestedServiceType,
            SuggestedArt = suggestedArt,
            SuggestedPatientName = suggestedPatientName
        };
    }

    public void MarkAssigned(Guid assignedByUserId, Guid resultingServiceId)
    {
        IsAssigned = true;
        AssignedByUserId = assignedByUserId;
        ResultingServiceId = resultingServiceId;
        Touch();
    }
}
