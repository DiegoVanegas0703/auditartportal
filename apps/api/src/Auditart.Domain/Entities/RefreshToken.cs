using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch();
    }
}
