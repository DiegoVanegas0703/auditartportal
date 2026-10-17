using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class WhatsAppOutboundLog : Entity
{
    public Guid? AuditServiceId { get; private set; }
    public string PhoneE164 { get; private set; } = string.Empty;
    public string TemplateName { get; private set; } = string.Empty;
    public string? BodyPreview { get; private set; }
    public string Status { get; private set; } = "Pending";
    public string? ProviderMessageId { get; private set; }
    public string? Error { get; private set; }
    public DateTime SentAtUtc { get; private set; } = DateTime.UtcNow;

    private WhatsAppOutboundLog() { }

    public static WhatsAppOutboundLog Create(
        Guid? auditServiceId,
        string phoneE164,
        string templateName,
        string? bodyPreview)
    {
        return new WhatsAppOutboundLog
        {
            AuditServiceId = auditServiceId,
            PhoneE164 = phoneE164,
            TemplateName = templateName,
            BodyPreview = bodyPreview,
            Status = "Pending",
            SentAtUtc = DateTime.UtcNow
        };
    }

    public void MarkSent(string? providerMessageId)
    {
        Status = "Sent";
        ProviderMessageId = providerMessageId;
        Touch();
    }

    public void MarkFailed(string error)
    {
        Status = "Failed";
        Error = error.Length > 2000 ? error[..2000] : error;
        Touch();
    }
}
