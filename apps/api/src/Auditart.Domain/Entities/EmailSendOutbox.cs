using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class EmailSendOutbox : Entity
{
    public Guid EmailMessageId { get; private set; }
    public EmailMessage? EmailMessage { get; private set; }

    public EmailOutboxState State { get; private set; } = EmailOutboxState.Pending;
    public DateTime AvailableAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LockedUntilUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private EmailSendOutbox() { }

    public static EmailSendOutbox Create(Guid emailMessageId) =>
        new()
        {
            EmailMessageId = emailMessageId,
            State = EmailOutboxState.Pending,
            AvailableAtUtc = DateTime.UtcNow
        };

    public bool TryClaim(TimeSpan lockDuration)
    {
        if (State is not EmailOutboxState.Pending)
            return false;
        if (LockedUntilUtc is not null && LockedUntilUtc > DateTime.UtcNow)
            return false;
        if (AvailableAtUtc > DateTime.UtcNow)
            return false;

        State = EmailOutboxState.Processing;
        LockedUntilUtc = DateTime.UtcNow.Add(lockDuration);
        AttemptCount++;
        Touch();
        return true;
    }

    public void MarkSent()
    {
        State = EmailOutboxState.Sent;
        CompletedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = null;
        LastError = null;
        Touch();
    }

    public void MarkFailed(string error, TimeSpan? retryAfter = null)
    {
        LastError = error.Length > 2000 ? error[..2000] : error;
        LockedUntilUtc = null;
        if (AttemptCount >= 8)
        {
            State = EmailOutboxState.Failed;
            CompletedAtUtc = DateTime.UtcNow;
        }
        else
        {
            State = EmailOutboxState.Pending;
            AvailableAtUtc = DateTime.UtcNow.Add(retryAfter ?? TimeSpan.FromMinutes(Math.Min(30, AttemptCount * 2)));
        }

        Touch();
    }
}
