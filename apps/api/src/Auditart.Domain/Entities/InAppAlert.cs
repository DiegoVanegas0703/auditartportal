using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class InAppAlert : Entity
{
    public Guid AuditServiceId { get; private set; }
    public AuditService? AuditService { get; private set; }
    public Guid UserId { get; private set; }
    public User? User { get; private set; }
    public InAppAlertKind Kind { get; private set; }
    public AuditStatus ServiceStatus { get; private set; }
    public AuditQueue Queue { get; private set; }
    public DateTime DeadlineUtc { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    private InAppAlert() { }

    public static InAppAlert Create(
        Guid auditServiceId,
        Guid userId,
        InAppAlertKind kind,
        AuditStatus serviceStatus,
        AuditQueue queue,
        DateTime deadlineUtc,
        string message)
    {
        return new InAppAlert
        {
            AuditServiceId = auditServiceId,
            UserId = userId,
            Kind = kind,
            ServiceStatus = serviceStatus,
            Queue = queue,
            DeadlineUtc = deadlineUtc,
            Message = message.Trim(),
            IsRead = false
        };
    }

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Resolve()
    {
        ResolvedAtUtc = DateTime.UtcNow;
        Touch();
    }
}
