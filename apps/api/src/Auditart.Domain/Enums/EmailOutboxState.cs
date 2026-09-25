namespace Auditart.Domain.Enums;

public enum EmailOutboxState
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3
}
