using Auditart.Domain.Common;
using Auditart.Domain.Enums;

namespace Auditart.Domain.Entities;

public class User : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? GoogleSubjectId { get; private set; }
    public UserRole Role { get; private set; }
    public AuditQueue? DefaultQueue { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAtUtc { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ICollection<AuditService> AssignedServices { get; private set; } = new List<AuditService>();

    private User() { }

    public static User Create(
        string name,
        string email,
        UserRole role,
        AuditQueue? defaultQueue = null,
        string? googleSubjectId = null)
    {
        return new User
        {
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            DefaultQueue = defaultQueue,
            GoogleSubjectId = googleSubjectId
        };
    }

    public void LinkGoogle(string googleSubjectId)
    {
        GoogleSubjectId = googleSubjectId;
        Touch();
    }

    public void UpdateRole(UserRole role, AuditQueue? defaultQueue = null)
    {
        Role = role;
        DefaultQueue = defaultQueue;
        Touch();
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
