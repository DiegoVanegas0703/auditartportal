using Auditart.Application.Abstractions;
using Auditart.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Triage;

public sealed class EmailOutboxProcessor
{
    private readonly IAppDbContext _db;
    private readonly IGmailChannelRegistry _channels;
    private readonly GmailIngestionService _ingestion;
    private readonly IObjectStorage _storage;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(
        IAppDbContext db,
        IGmailChannelRegistry channels,
        GmailIngestionService ingestion,
        IObjectStorage storage,
        ILogger<EmailOutboxProcessor> logger)
    {
        _db = db;
        _channels = channels;
        _ingestion = ingestion;
        _storage = storage;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.EmailSendOutbox
            .Include(o => o.EmailMessage)!
            .ThenInclude(m => m!.EmailThread)
            .Include(o => o.EmailMessage)!
            .ThenInclude(m => m!.Attachments)
            .Where(o =>
                o.State == EmailOutboxState.Pending &&
                o.AvailableAtUtc <= DateTime.UtcNow &&
                (o.LockedUntilUtc == null || o.LockedUntilUtc < DateTime.UtcNow))
            .OrderBy(o => o.AvailableAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var item in pending)
        {
            if (!item.TryClaim(TimeSpan.FromMinutes(2)))
                continue;

            var message = item.EmailMessage;
            if (message is null || message.EmailThread is null)
            {
                item.MarkFailed("Mensaje u hilo ausente.");
                await _db.SaveChangesAsync(cancellationToken);
                continue;
            }

            message.MarkSending();
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                var gmail = ResolveGmail(message.EmailThread);
                var internetMessageId = message.InternetMessageId
                    ?? $"<auditart.{message.Id:N}@auditart.local>";

                var outboundAttachments = new List<GmailOutboundAttachment>();
                foreach (var attachment in message.Attachments)
                {
                    var download = await _storage.DownloadAsync(attachment.S3Key, cancellationToken);
                    await using var content = download.Content;
                    await using var ms = new MemoryStream();
                    await content.CopyToAsync(ms, cancellationToken);
                    outboundAttachments.Add(new GmailOutboundAttachment(
                        attachment.FileName,
                        attachment.ContentType,
                        ms.ToArray()));
                }

                var result = await gmail.SendReplyAsync(
                    new GmailSendRequest(
                        message.EmailThread.ProviderThreadId,
                        message.FromAddress,
                        message.ToAddresses,
                        message.CcAddresses,
                        message.Subject,
                        message.BodyText,
                        message.BodyHtml,
                        message.InReplyTo,
                        message.ReferencesHeader,
                        internetMessageId,
                        outboundAttachments),
                    cancellationToken);

                message.MarkSent(result.ProviderMessageId, result.InternetMessageId ?? internetMessageId);
                if (!string.IsNullOrWhiteSpace(result.ThreadId) &&
                    message.EmailThread.ProviderThreadId.StartsWith("new-", StringComparison.OrdinalIgnoreCase))
                {
                    message.EmailThread.BindProviderThreadId(result.ThreadId);
                }

                item.MarkSent();
                await _db.SaveChangesAsync(cancellationToken);
                await _ingestion.RefreshRequestSummaryAsync(message.EmailThread.EmailRequestId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error enviando mensaje {MessageId}", message.Id);
                message.MarkFailed(ex.Message);
                item.MarkFailed(ex.Message);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private IGmailInboxService ResolveGmail(Domain.Entities.EmailThread thread)
    {
        var mailbox = thread.Mailbox.Trim().ToLowerInvariant();
        foreach (EmailChannel channel in Enum.GetValues<EmailChannel>())
        {
            if (_channels.GetMailbox(channel) == mailbox)
                return _channels.GetInbox(channel);
        }

        return _channels.GetInbox(EmailChannel.General);
    }
}
