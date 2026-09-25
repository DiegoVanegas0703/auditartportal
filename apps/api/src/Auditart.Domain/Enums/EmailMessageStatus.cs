namespace Auditart.Domain.Enums;

public enum EmailMessageStatus
{
    Received = 0,
    PendingSend = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4
}
