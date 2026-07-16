using Auditart.Domain.Entities;
using Auditart.Domain.Enums;

namespace Auditart.Application.Abstractions;

public interface IAppDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<AuditService> AuditServices { get; }
    IQueryable<IncomingEmail> IncomingEmails { get; }
    IQueryable<ServiceAttachment> ServiceAttachments { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken();
    string HashToken(string rawToken);
}

public interface IGoogleAuthService
{
    Task<GoogleUserInfo> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleUserInfo(
    string SubjectId,
    string Email,
    string Name,
    bool EmailVerified);

public interface IObjectStorage
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    Task<string> GetPresignedUrlAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public interface IGmailInboxService
{
    Task<IReadOnlyList<GmailMessageDto>> FetchUnreadAsync(CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);
}

public sealed record GmailMessageDto(
    string MessageId,
    string From,
    string Subject,
    string Body,
    DateTime ReceivedAtUtc,
    IReadOnlyList<GmailAttachmentDto> Attachments);

public sealed record GmailAttachmentDto(
    string AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    byte[] Content);

public interface IPermissionService
{
    bool CanTriage(UserRole role);
    bool CanOperateBoard(UserRole role);
    bool CanBill(UserRole role);
    bool CanManageUsers(UserRole role);
    bool SeesAllQueues(UserRole role);
}
