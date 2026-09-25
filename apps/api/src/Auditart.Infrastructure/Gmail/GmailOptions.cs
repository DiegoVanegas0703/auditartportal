namespace Auditart.Infrastructure.Gmail;

public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    public bool Enabled { get; init; }
    public bool PollingEnabled { get; init; } = true;
    public int PollingIntervalMinutes { get; init; } = 5;
    public string Mailbox { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    /// <summary>Consulta Gmail. Por defecto: no leídos del inbox.</summary>
    public string SearchQuery { get; init; } = "is:unread in:inbox";
    public int MaxResults { get; init; } = 50;
    public long MaxAttachmentBytes { get; init; } = 25 * 1024 * 1024;
}
