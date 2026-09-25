using Auditart.Domain.Entities;
using Auditart.Domain.Enums;

namespace Auditart.Application.Abstractions;

public interface IAppDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<AuditService> AuditServices { get; }
    IQueryable<IncomingEmail> IncomingEmails { get; }
    IQueryable<ServiceAttachment> ServiceAttachments { get; }
    IQueryable<ServiceStatusHistory> ServiceStatusHistories { get; }
    IQueryable<SlaRule> SlaRules { get; }
    IQueryable<InAppAlert> InAppAlerts { get; }
    IQueryable<Prestador> Prestadores { get; }
    IQueryable<Paciente> Pacientes { get; }
    IQueryable<PrecioCatalogo> PreciosCatalogo { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<EmailRequest> EmailRequests { get; }
    IQueryable<EmailThread> EmailThreads { get; }
    IQueryable<EmailMessage> EmailMessages { get; }
    IQueryable<EmailAttachment> EmailAttachments { get; }
    IQueryable<EmailSendOutbox> EmailSendOutbox { get; }
    IQueryable<GmailSyncState> GmailSyncStates { get; }

    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void ClearChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
    (string RawToken, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken();
    string HashToken(string rawToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IObjectStorage
{
    Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default);

    Task<ObjectDownload> DownloadAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record ObjectDownload(Stream Content, string? ContentType);

public interface IGmailInboxService
{
    Task<IReadOnlyList<GmailMessageDto>> FetchUnreadAsync(CancellationToken cancellationToken = default);
    Task<GmailMessageDto?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default);
    Task<string?> GetProfileHistoryIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista messageIds añadidos desde startHistoryId.
    /// Si HistoryExpired=true, el cliente debe reiniciar con una sync completa.
    /// </summary>
    Task<GmailHistoryResult> FetchHistoryAddedAsync(
        string startHistoryId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea labels de usuario si no existen y aplica/quita por nombre en el hilo de Gmail.
    /// </summary>
    Task ModifyThreadLabelsByNameAsync(
        string threadId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual que <see cref="ModifyThreadLabelsByNameAsync"/> pero sobre un mensaje concreto.
    /// </summary>
    Task ModifyMessageLabelsByNameAsync(
        string messageId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default);

    Task<GmailSendResult> SendReplyAsync(GmailSendRequest request, CancellationToken cancellationToken = default);
}

public sealed record GmailHistoryResult(
    IReadOnlyList<string> MessageIds,
    string? LatestHistoryId,
    bool HistoryExpired);

public sealed record GmailMessageDto(
    string MessageId,
    string ThreadId,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string? ReplyTo,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTime ReceivedAtUtc,
    string? InternetMessageId,
    string? InReplyTo,
    string? References,
    IReadOnlyList<GmailAttachmentDto> Attachments)
{
    // Compatibilidad con código que aún usa Body.
    public string Body => BodyText;
}

public sealed record GmailAttachmentDto(
    string AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    byte[] Content);

public sealed record GmailSendRequest(
    string ThreadId,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string BodyText,
    string? BodyHtml,
    string? InReplyTo,
    string? References,
    string InternetMessageId,
    IReadOnlyList<GmailOutboundAttachment>? Attachments = null);

public sealed record GmailOutboundAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record GmailSendResult(
    string ProviderMessageId,
    string ThreadId,
    string? InternetMessageId);

public interface IPermissionService
{
    bool CanTriage(UserRole role);
    bool CanOperateBoard(UserRole role);
    bool CanBill(UserRole role);
    bool CanManageUsers(UserRole role);
    bool CanViewReports(UserRole role);
    bool SeesAllQueues(UserRole role);
    bool CanAccessChannel(UserRole role, EmailChannel channel);
}

public interface IGmailChannelRegistry
{
    IGmailInboxService GetInbox(EmailChannel channel);
    string GetMailbox(EmailChannel channel);
    IReadOnlyList<EmailChannel> GetEnabledChannels();
}
