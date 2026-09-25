using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

/// <summary>
/// Unidad de triage / requerimiento. Puede agrupar uno o más hilos de Gmail.
/// </summary>
public class EmailRequest : Entity
{
    public EmailRequestState State { get; private set; } = EmailRequestState.Pending;
    public EmailChannel Channel { get; private set; } = EmailChannel.General;
    public string Subject { get; private set; } = string.Empty;
    public DateTime LastMessageAtUtc { get; private set; } = DateTime.UtcNow;
    public int MessageCount { get; private set; }
    public int AttachmentCount { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public List<string> Participants { get; private set; } = [];

    /// <summary>Token de concurrencia optimista (PostgreSQL xmin).</summary>
    public uint Version { get; private set; }

    public Guid? AuditServiceId { get; private set; }
    public AuditService? AuditService { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public Guid? IgnoredByUserId { get; private set; }
    public DateTime? IgnoredAtUtc { get; private set; }

    public ICollection<EmailThread> Threads { get; private set; } = new List<EmailThread>();

    private EmailRequest() { }

    public static EmailRequest Create(
        string subject,
        DateTime lastMessageAtUtc,
        EmailChannel channel = EmailChannel.General)
    {
        return new EmailRequest
        {
            Subject = NormalizeSubject(subject),
            LastMessageAtUtc = lastMessageAtUtc,
            State = EmailRequestState.Pending,
            Channel = channel
        };
    }

    public void RefreshSummary(
        string subject,
        DateTime lastMessageAtUtc,
        int messageCount,
        int attachmentCount,
        IEnumerable<string> participants)
    {
        Subject = NormalizeSubject(subject);
        LastMessageAtUtc = lastMessageAtUtc;
        MessageCount = messageCount;
        AttachmentCount = attachmentCount;
        Participants = participants
            .Select(NormalizeParticipant)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .Take(40)
            .ToList();
        Touch();
    }

    public void SetTags(IEnumerable<string> tags)
    {
        Tags = NormalizeTags(tags);
        Touch();
    }

    public void Ignore(Guid? ignoredByUserId = null)
    {
        if (State == EmailRequestState.Assigned)
            throw new InvalidOperationException("No se puede ignorar un requerimiento ya derivado.");

        State = EmailRequestState.Ignored;
        IgnoredAtUtc = DateTime.UtcNow;
        IgnoredByUserId = ignoredByUserId;
        Touch();
    }

    public void Restore()
    {
        if (State == EmailRequestState.Assigned)
            throw new InvalidOperationException("No se puede restaurar un requerimiento ya derivado.");

        State = EmailRequestState.Pending;
        IgnoredAtUtc = null;
        IgnoredByUserId = null;
        Touch();
    }

    public void MarkAssigned(Guid auditServiceId, Guid assignedByUserId)
    {
        if (State == EmailRequestState.Assigned)
            throw new InvalidOperationException("El requerimiento ya fue derivado.");
        if (State == EmailRequestState.Ignored)
            throw new InvalidOperationException("Restaurá el requerimiento antes de derivarlo.");

        State = EmailRequestState.Assigned;
        AuditServiceId = auditServiceId;
        AssignedByUserId = assignedByUserId;
        AssignedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Absorb(EmailRequest other)
    {
        if (other.Id == Id)
            throw new InvalidOperationException("No se puede unir un requerimiento consigo mismo.");
        if (State == EmailRequestState.Assigned || other.State == EmailRequestState.Assigned)
        {
            if (AuditServiceId != other.AuditServiceId)
                throw new InvalidOperationException(
                    "No se pueden unir requerimientos vinculados a auditorías distintas.");
        }

        foreach (var thread in other.Threads.ToList())
            thread.MoveToRequest(this);

        other.Threads.Clear();
        Tags = NormalizeTags(Tags.Concat(other.Tags));
        if (LastMessageAtUtc < other.LastMessageAtUtc)
        {
            LastMessageAtUtc = other.LastMessageAtUtc;
            Subject = other.Subject;
        }

        Touch();
    }

    private static string NormalizeSubject(string subject) =>
        string.IsNullOrWhiteSpace(subject) ? "(Sin asunto)" : subject.Trim();

    private static string NormalizeParticipant(string value) => value.Trim().ToLowerInvariant();

    private static List<string> NormalizeTags(IEnumerable<string> tags) =>
        tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Where(tag => tag.Length <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .OrderBy(tag => tag)
            .ToList();
}
