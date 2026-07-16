using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class ServiceStatusHistory : Entity
{
    public Guid AuditServiceId { get; private set; }
    public AuditService AuditService { get; private set; } = null!;
    public AuditStatus? FromStatus { get; private set; }
    public AuditStatus ToStatus { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public string? Reason { get; private set; }

    private ServiceStatusHistory() { }

    public static ServiceStatusHistory Create(
        Guid auditServiceId,
        AuditStatus? from,
        AuditStatus to,
        Guid? changedByUserId,
        string? reason)
    {
        return new ServiceStatusHistory
        {
            AuditServiceId = auditServiceId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Reason = reason
        };
    }
}
