using Auditart.Application.Abstractions;

using Auditart.Domain.Entities;

using Auditart.Domain.Enums;

using Microsoft.EntityFrameworkCore;



namespace Auditart.Application.Triage;



public sealed record GmailSyncResult(

    int Fetched,

    int Inserted,

    int Skipped,

    int Failed,

    IReadOnlyList<string> Errors);



public sealed class GmailIngestionService

{

    private static readonly SemaphoreSlim SyncLock = new(1, 1);



    private readonly IAppDbContext _db;

    private readonly IGmailChannelRegistry _channels;

    private readonly IObjectStorage _storage;



    public GmailIngestionService(

        IAppDbContext db,

        IGmailChannelRegistry channels,

        IObjectStorage storage)

    {

        _db = db;

        _channels = channels;

        _storage = storage;

    }



    public async Task<GmailSyncResult> SyncUnreadAsync(CancellationToken cancellationToken = default)

    {

        await SyncLock.WaitAsync(cancellationToken);

        try

        {

            var fetched = 0;

            var inserted = 0;

            var skipped = 0;

            var errors = new List<string>();



            foreach (var channel in _channels.GetEnabledChannels())

            {

                var result = await SyncChannelAsync(channel, cancellationToken);

                fetched += result.Fetched;

                inserted += result.Inserted;

                skipped += result.Skipped;

                errors.AddRange(result.Errors);

            }



            return new GmailSyncResult(fetched, inserted, skipped, errors.Count, errors);

        }

        finally

        {

            SyncLock.Release();

        }

    }



    private async Task<GmailSyncResult> SyncChannelAsync(

        EmailChannel channel,

        CancellationToken cancellationToken)

    {

        var mailbox = _channels.GetMailbox(channel);

        var gmail = _channels.GetInbox(channel);



        var state = await _db.GmailSyncStates

            .FirstOrDefaultAsync(s => s.Mailbox == mailbox, cancellationToken);

        if (state is null)

        {

            state = GmailSyncState.Create(mailbox);

            _db.Add(state);

            await _db.SaveChangesAsync(cancellationToken);

        }



        var messageIds = new HashSet<string>(StringComparer.Ordinal);

        var usedHistory = false;



        if (!string.IsNullOrWhiteSpace(state.LastHistoryId))

        {

            var history = await gmail.FetchHistoryAddedAsync(state.LastHistoryId, cancellationToken);

            if (history.HistoryExpired)

            {

                state.ClearHistory();

                await _db.SaveChangesAsync(cancellationToken);

            }

            else

            {

                usedHistory = true;

                foreach (var id in history.MessageIds)

                    messageIds.Add(id);

            }

        }



        var unread = await gmail.FetchUnreadAsync(cancellationToken);

        foreach (var msg in unread)

            messageIds.Add(msg.MessageId);



        var inserted = 0;

        var skipped = 0;

        var errors = new List<string>();

        var fetched = messageIds.Count;



        foreach (var messageId in messageIds)

        {

            try

            {

                var exists = await _db.EmailMessages

                    .AnyAsync(m => m.ProviderMessageId == messageId, cancellationToken);

                if (exists)

                {

                    skipped++;

                    await TryMarkAsReadAsync(gmail, messageId, errors, cancellationToken);

                    continue;

                }



                var message = unread.FirstOrDefault(m => m.MessageId == messageId)

                    ?? await gmail.GetMessageAsync(messageId, cancellationToken);

                if (message is null)

                {

                    skipped++;

                    continue;

                }



                var existingIncoming = await _db.IncomingEmails

                    .FirstOrDefaultAsync(e => e.GmailMessageId == messageId, cancellationToken);



                await PersistMessageAsync(channel, mailbox, message, cancellationToken, existingIncoming);

                inserted++;

                await TryMarkAsReadAsync(gmail, messageId, errors, cancellationToken);

            }

            catch (Exception ex) when (ex is not OperationCanceledException)

            {

                _db.ClearChanges();

                errors.Add($"{mailbox}/{messageId}: {ex.Message}");

            }

        }



        await UpdateSyncStateAsync(gmail, mailbox, errors, cancellationToken);



        if (!usedHistory && inserted == 0 && skipped == 0 && errors.Count == 0)

            await UpdateSyncStateAsync(gmail, mailbox, errors, cancellationToken);



        return new GmailSyncResult(fetched, inserted, skipped, errors.Count, errors);

    }



    public async Task<GmailSyncResult> BackfillFromIncomingAsync(CancellationToken cancellationToken = default)

    {

        await SyncLock.WaitAsync(cancellationToken);

        try

        {

            var orphans = await _db.IncomingEmails

                .Where(e => !_db.EmailMessages.Any(m => m.IncomingEmailId == e.Id))

                .OrderByDescending(e => e.ReceivedAtUtc)

                .Take(200)

                .ToListAsync(cancellationToken);



            var inserted = 0;

            var skipped = 0;

            var errors = new List<string>();

            var channel = EmailChannel.General;

            var mailbox = _channels.GetMailbox(channel);

            var gmail = _channels.GetInbox(channel);



            foreach (var email in orphans)

            {

                try

                {

                    var gmailMessage = await gmail.GetMessageAsync(email.GmailMessageId, cancellationToken);

                    if (gmailMessage is null)

                    {

                        await PersistLegacyEmailAsync(channel, mailbox, email, email.GmailMessageId, cancellationToken);

                        inserted++;

                        continue;

                    }



                    await PersistMessageAsync(channel, mailbox, gmailMessage, cancellationToken, email);

                    inserted++;

                }

                catch (Exception ex) when (ex is not OperationCanceledException)

                {

                    _db.ClearChanges();

                    errors.Add($"{email.GmailMessageId}: {Unwrap(ex)}");

                }

            }



            return new GmailSyncResult(orphans.Count, inserted, skipped, errors.Count, errors);

        }

        finally

        {

            SyncLock.Release();

        }

    }



    private async Task PersistMessageAsync(

        EmailChannel channel,

        string mailbox,

        GmailMessageDto message,

        CancellationToken cancellationToken,

        IncomingEmail? existingIncoming = null)

    {

        var thread = await _db.EmailThreads

            .Include(t => t.EmailRequest)

            .FirstOrDefaultAsync(

                t => t.Mailbox == mailbox && t.ProviderThreadId == message.ThreadId,

                cancellationToken);



        EmailRequest request;

        if (thread?.EmailRequest is not null)

        {

            request = thread.EmailRequest;

        }

        else if (thread is null)

        {

            request = EmailRequest.Create(message.Subject, message.ReceivedAtUtc, channel);

            ApplyLegacyState(request, existingIncoming);

            _db.Add(request);

            thread = EmailThread.Create(

                request.Id,

                mailbox,

                message.ThreadId,

                message.Subject,

                message.ReceivedAtUtc);

            _db.Add(thread);

        }

        else

        {

            request = await _db.EmailRequests.FirstAsync(r => r.Id == thread.EmailRequestId, cancellationToken);

        }



        IncomingEmail incoming;

        if (existingIncoming is not null)

        {

            incoming = existingIncoming;

        }

        else

        {

            incoming = IncomingEmail.Create(

                message.MessageId,

                message.From,

                message.Subject,

                message.BodyText,

                message.ReceivedAtUtc,

                message.Attachments.Count);

            _db.Add(incoming);

        }



        var emailMessage = EmailMessage.CreateInbound(

            thread.Id,

            message.MessageId,

            message.From,

            message.Subject,

            message.BodyText,

            message.ReceivedAtUtc,

            message.To,

            message.Cc,

            message.BodyHtml,

            message.InternetMessageId,

            message.InReplyTo,

            message.References,

            message.ReplyTo,

            incoming.Id);

        _db.Add(emailMessage);



        foreach (var attachment in message.Attachments)

        {

            await using var content = new MemoryStream(attachment.Content, writable: false);

            var key = await _storage.UploadAsync(

                content,

                attachment.FileName,

                attachment.ContentType,

                $"gmail/{message.MessageId}",

                cancellationToken);



            var serviceAttachment = ServiceAttachment.Create(

                attachment.FileName,

                attachment.ContentType,

                attachment.SizeBytes,

                key,

                s3Bucket: null,

                incomingEmailId: incoming.Id,

                auditServiceId: request.State == EmailRequestState.Assigned

                    ? request.AuditServiceId

                    : null);

            _db.Add(serviceAttachment);



            _db.Add(EmailAttachment.Create(

                emailMessage.Id,

                attachment.FileName,

                attachment.ContentType,

                attachment.SizeBytes,

                key,

                providerAttachmentId: Truncate(attachment.AttachmentId, 1024),

                serviceAttachmentId: serviceAttachment.Id));

        }



        await _db.SaveChangesAsync(cancellationToken);

        await RefreshRequestSummaryAsync(request.Id, cancellationToken);

    }



    private async Task PersistLegacyEmailAsync(

        EmailChannel channel,

        string mailbox,

        IncomingEmail email,

        string threadId,

        CancellationToken cancellationToken)

    {

        var request = EmailRequest.Create(email.Subject, email.ReceivedAtUtc, channel);

        if (email.IsIgnored)

            request.Ignore(email.IgnoredByUserId);

        if (email.IsAssigned && email.ResultingServiceId is Guid serviceId)

            request.MarkAssigned(serviceId, email.AssignedByUserId ?? Guid.Empty);

        request.SetTags(email.Tags);

        _db.Add(request);



        var thread = EmailThread.Create(

            request.Id,

            mailbox,

            threadId,

            email.Subject,

            email.ReceivedAtUtc);

        _db.Add(thread);



        var message = EmailMessage.CreateInbound(

            thread.Id,

            email.GmailMessageId,

            email.FromAddress,

            email.Subject,

            email.Body,

            email.ReceivedAtUtc,

            incomingEmailId: email.Id);

        _db.Add(message);



        var attachments = await _db.ServiceAttachments

            .Where(a => a.IncomingEmailId == email.Id)

            .ToListAsync(cancellationToken);

        foreach (var attachment in attachments)

        {

            _db.Add(EmailAttachment.Create(

                message.Id,

                attachment.FileName,

                attachment.ContentType,

                attachment.SizeBytes,

                attachment.S3Key,

                attachment.S3Bucket,

                serviceAttachmentId: attachment.Id));

        }



        await _db.SaveChangesAsync(cancellationToken);

        await RefreshRequestSummaryAsync(request.Id, cancellationToken);

    }



    private static void ApplyLegacyState(EmailRequest request, IncomingEmail? legacy)

    {

        if (legacy is null) return;

        request.SetTags(legacy.Tags);

        if (legacy.IsIgnored)

            request.Ignore(legacy.IgnoredByUserId);

        if (legacy.IsAssigned && legacy.ResultingServiceId is Guid serviceId)

            request.MarkAssigned(serviceId, legacy.AssignedByUserId ?? Guid.Empty);

    }



    public async Task RefreshRequestSummaryAsync(Guid requestId, CancellationToken cancellationToken)

    {

        var request = await _db.EmailRequests

            .Include(r => r.Threads)

            .ThenInclude(t => t.Messages)

            .ThenInclude(m => m.Attachments)

            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null) return;



        var messages = request.Threads.SelectMany(t => t.Messages).ToList();

        if (messages.Count == 0) return;



        var last = messages.OrderByDescending(m => m.OccurredAtUtc).First();

        var participants = messages

            .SelectMany(m => new[] { m.FromAddress }.Concat(m.ToAddresses).Concat(m.CcAddresses));

        request.RefreshSummary(

            last.Subject,

            last.OccurredAtUtc,

            messages.Count,

            messages.Sum(m => m.Attachments.Count),

            participants);



        var thread = request.Threads.FirstOrDefault(t => t.Id == last.EmailThreadId);

        thread?.TouchLastMessage(last.OccurredAtUtc, last.Subject);



        await _db.SaveChangesAsync(cancellationToken);

    }



    private async Task UpdateSyncStateAsync(

        IGmailInboxService gmail,

        string mailbox,

        List<string> errors,

        CancellationToken cancellationToken)

    {

        var state = await _db.GmailSyncStates

            .FirstOrDefaultAsync(s => s.Mailbox == mailbox, cancellationToken);

        if (state is null)

        {

            state = GmailSyncState.Create(mailbox);

            _db.Add(state);

        }



        try

        {

            var historyId = await gmail.GetProfileHistoryIdAsync(cancellationToken);

            if (errors.Count == 0)

                state.MarkSuccess(historyId);

            else

                state.MarkError(string.Join("; ", errors.Take(3)));

            await _db.SaveChangesAsync(cancellationToken);

        }

        catch

        {

            // No bloquear sync por estado.

        }

    }



    private static async Task TryMarkAsReadAsync(

        IGmailInboxService gmail,

        string messageId,

        ICollection<string> errors,

        CancellationToken cancellationToken)

    {

        try

        {

            await gmail.MarkAsReadAsync(messageId, cancellationToken);

        }

        catch (Exception ex) when (ex is not OperationCanceledException)

        {

            errors.Add($"{messageId}: guardado, pero Gmail no pudo marcarlo como leído: {ex.Message}");

        }

    }



    private static string? Truncate(string? value, int max) =>

        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];



    private static string Unwrap(Exception ex)

    {

        var current = ex;

        while (current.InnerException is not null)

            current = current.InnerException;

        return current.Message;

    }

}


