namespace Auditart.Infrastructure.Gmail;

public sealed class GmailChannelOptions
{
    public string Mailbox { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? RefreshToken { get; init; }
    public string SearchQuery { get; init; } = "is:unread in:inbox";
    public int MaxResults { get; init; } = 50;
    public long MaxAttachmentBytes { get; init; } = 25 * 1024 * 1024;
}
