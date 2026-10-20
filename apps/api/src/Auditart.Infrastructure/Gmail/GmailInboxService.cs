using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Auditart.Application.Abstractions;
using Auditart.Application.Triage;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace Auditart.Infrastructure.Gmail;

public sealed class GmailInboxService : IGmailInboxService, IDisposable
{
    // Cada guardado de borrador crea un mensaje nuevo en el historial de Gmail.
    private static readonly HashSet<string> IgnoredHistoryLabels =
        new(StringComparer.OrdinalIgnoreCase) { "DRAFT", "SPAM", "TRASH", "CHAT" };

    private readonly GmailOptions _options;
    private readonly GmailService _gmail;
    private readonly ConcurrentDictionary<string, string> _labelIdsByName =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _labelsLock = new(1, 1);
    private bool _labelsLoaded;

    public GmailInboxService(IOptions<GmailOptions> options)
    {
        _options = options.Value;
        ValidateOptions(_options);

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret
            },
            Scopes =
            [
                GmailService.Scope.GmailModify,
                GmailService.Scope.GmailSend
            ]
        });

        var credential = new UserCredential(
            flow,
            _options.Mailbox,
            new TokenResponse { RefreshToken = _options.RefreshToken });

        _gmail = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Auditart Portal"
        });
    }

    public async Task<IReadOnlyList<string>> ListUnreadIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(_options.MaxResults, 1, 100);
        var query = string.IsNullOrWhiteSpace(_options.SearchQuery)
            ? "is:unread in:inbox"
            : _options.SearchQuery.Trim();

        var messageIds = new List<string>();
        string? pageToken = null;
        do
        {
            var listRequest = _gmail.Users.Messages.List("me");
            listRequest.Q = query;
            listRequest.MaxResults = pageSize;
            listRequest.PageToken = pageToken;

            var list = await listRequest.ExecuteAsync(cancellationToken);
            if (list.Messages is { Count: > 0 })
                messageIds.AddRange(list.Messages.Select(message => message.Id));

            pageToken = list.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken) && messageIds.Count < 500);

        return messageIds;
    }

    public async Task<GmailMessageDto?> GetMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var getRequest = _gmail.Users.Messages.Get("me", messageId);
            getRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var message = await getRequest.ExecuteAsync(cancellationToken);
            return await MapMessageAsync(message, cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode == HttpStatusCode.NotFound ||
            ex.Error?.Code == 404)
        {
            // Mensaje borrado / history stale: el caller lo trata como skip.
            return null;
        }
    }

    public async Task<string?> GetThreadIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = _gmail.Users.Messages.Get("me", messageId);
            request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Minimal;
            var message = await request.ExecuteAsync(cancellationToken);
            return message.ThreadId;
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode == HttpStatusCode.NotFound ||
            ex.Error?.Code == 404)
        {
            return null;
        }
    }

    public async Task<string?> GetProfileHistoryIdAsync(CancellationToken cancellationToken = default)
    {
        var profile = await _gmail.Users.GetProfile("me").ExecuteAsync(cancellationToken);
        return profile.HistoryId?.ToString();
    }

    public async Task<GmailHistoryResult> FetchHistoryAddedAsync(
        string startHistoryId,
        CancellationToken cancellationToken = default)
    {
        if (!ulong.TryParse(startHistoryId, out var startId) || startId == 0)
            return new GmailHistoryResult([], null, HistoryExpired: true);

        try
        {
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            string? pageToken = null;
            ulong? latest = null;

            do
            {
                var request = _gmail.Users.History.List("me");
                request.StartHistoryId = startId;
                request.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;
                request.PageToken = pageToken;
                request.MaxResults = 100;

                var response = await request.ExecuteAsync(cancellationToken);
                latest = response.HistoryId ?? latest;

                if (response.History is { Count: > 0 })
                {
                    foreach (var entry in response.History)
                    {
                        if (entry.MessagesAdded is null) continue;
                        foreach (var added in entry.MessagesAdded)
                        {
                            if (string.IsNullOrWhiteSpace(added.Message?.Id))
                                continue;
                            if (added.Message.LabelIds?.Any(IgnoredHistoryLabels.Contains) == true)
                                continue;
                            messageIds.Add(added.Message.Id);
                        }
                    }
                }

                pageToken = response.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            return new GmailHistoryResult(
                messageIds.ToArray(),
                latest?.ToString(),
                HistoryExpired: false);
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode == HttpStatusCode.NotFound ||
            ex.Error?.Code == 404)
        {
            return new GmailHistoryResult([], null, HistoryExpired: true);
        }
    }

    public async Task MarkAsReadAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = _gmail.Users.Messages.Modify(
                new ModifyMessageRequest { RemoveLabelIds = ["UNREAD"] },
                "me",
                messageId);
            await request.ExecuteAsync(cancellationToken);
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode == HttpStatusCode.NotFound ||
            ex.Error?.Code == 404)
        {
            // Ya no existe en Gmail; nada que marcar.
        }
    }

    public async Task ModifyThreadLabelsByNameAsync(
        string threadId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            return;

        var (addIds, removeIds) = await ResolveLabelIdsAsync(
            addLabelNames,
            removeLabelNames,
            createMissing: true,
            cancellationToken);

        if (addIds.Count == 0 && removeIds.Count == 0)
            return;

        var request = _gmail.Users.Threads.Modify(
            new ModifyThreadRequest
            {
                AddLabelIds = addIds,
                RemoveLabelIds = removeIds
            },
            "me",
            threadId);
        await request.ExecuteAsync(cancellationToken);
    }

    public async Task ModifyMessageLabelsByNameAsync(
        string messageId,
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return;

        var (addIds, removeIds) = await ResolveLabelIdsAsync(
            addLabelNames,
            removeLabelNames,
            createMissing: true,
            cancellationToken);

        if (addIds.Count == 0 && removeIds.Count == 0)
            return;

        var request = _gmail.Users.Messages.Modify(
            new ModifyMessageRequest
            {
                AddLabelIds = addIds,
                RemoveLabelIds = removeIds
            },
            "me",
            messageId);
        await request.ExecuteAsync(cancellationToken);
    }

    public async Task<GmailSendResult> SendReplyAsync(
        GmailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var mime = BuildMime(request);
        var raw = EncodeBase64Url(Encoding.UTF8.GetBytes(mime));
        var message = new Message { Raw = raw };

        // Hilos locales "new-*" aún no existen en Gmail: omitir ThreadId.
        if (!string.IsNullOrWhiteSpace(request.ThreadId) &&
            !request.ThreadId.StartsWith("new-", StringComparison.OrdinalIgnoreCase))
        {
            message.ThreadId = request.ThreadId;
        }

        var sent = await _gmail.Users.Messages.Send(message, "me").ExecuteAsync(cancellationToken);

        return new GmailSendResult(
            sent.Id,
            sent.ThreadId ?? request.ThreadId,
            request.InternetMessageId);
    }

    private static string BuildMime(GmailSendRequest request)
    {
        // Valida direcciones
        _ = new MailAddress(ExtractEmail(request.From), ExtractDisplayName(request.From));
        foreach (var to in request.To)
            _ = new MailAddress(to);
        foreach (var cc in request.Cc)
            _ = new MailAddress(cc);

        var attachments = request.Attachments ?? [];
        var boundary = $"auditart_{Guid.NewGuid():N}";
        var sb = new StringBuilder();
        sb.Append("From: ").Append(request.From).Append("\r\n");
        sb.Append("To: ").Append(string.Join(", ", request.To)).Append("\r\n");
        if (request.Cc.Count > 0)
            sb.Append("Cc: ").Append(string.Join(", ", request.Cc)).Append("\r\n");
        sb.Append("Subject: ").Append(EncodeHeader(request.Subject)).Append("\r\n");
        sb.Append("Message-ID: ").Append(request.InternetMessageId).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(request.InReplyTo))
            sb.Append("In-Reply-To: ").Append(request.InReplyTo).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(request.References))
            sb.Append("References: ").Append(request.References).Append("\r\n");
        sb.Append("MIME-Version: 1.0\r\n");

        var bodyPart = BuildBodyPart(request);

        if (attachments.Count == 0)
        {
            sb.Append(bodyPart);
            return sb.ToString();
        }

        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n\r\n");
        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append(bodyPart).Append("\r\n");

        foreach (var attachment in attachments)
        {
            var safeName = (attachment.FileName ?? "archivo")
                .Replace("\"", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: ").Append(contentType).Append("; name=\"").Append(safeName).Append("\"\r\n");
            sb.Append("Content-Transfer-Encoding: base64\r\n");
            sb.Append("Content-Disposition: attachment; filename=\"").Append(safeName).Append("\"\r\n\r\n");
            sb.Append(Convert.ToBase64String(attachment.Content, Base64FormattingOptions.InsertLineBreaks));
            sb.Append("\r\n");
        }

        sb.Append("--").Append(boundary).Append("--\r\n");
        return sb.ToString();
    }

    private static readonly Regex DataImageSrc = new(
        "src\\s*=\\s*([\"'])data:(image/[a-zA-Z0-9.+-]+);base64,([A-Za-z0-9+/=\\s]+)\\1",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Headers + contenido del cuerpo. Las imágenes data: del HTML (p. ej. logo de la firma)
    /// se envían como partes inline con Content-ID: Gmail/Outlook bloquean data: URIs.
    /// </summary>
    private static string BuildBodyPart(GmailSendRequest request)
    {
        var sb = new StringBuilder();
        if (string.IsNullOrWhiteSpace(request.BodyHtml))
        {
            sb.Append("Content-Type: text/plain; charset=UTF-8\r\n");
            sb.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            sb.Append(request.BodyText);
            return sb.ToString();
        }

        var inline = new List<(string Cid, string ContentType, string Base64)>();
        var html = DataImageSrc.Replace(request.BodyHtml, match =>
        {
            var base64 = Regex.Replace(match.Groups[3].Value, "\\s+", string.Empty);
            var existing = inline.FindIndex(i => i.Base64 == base64);
            string cid;
            if (existing >= 0)
            {
                cid = inline[existing].Cid;
            }
            else
            {
                cid = $"img{inline.Count + 1}.{Guid.NewGuid():N}@auditart";
                inline.Add((cid, match.Groups[2].Value.ToLowerInvariant(), base64));
            }
            var quote = match.Groups[1].Value;
            return $"src={quote}cid:{cid}{quote}";
        });

        if (inline.Count == 0)
        {
            sb.Append("Content-Type: text/html; charset=UTF-8\r\n");
            sb.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
            sb.Append(html);
            return sb.ToString();
        }

        var related = $"auditart_rel_{Guid.NewGuid():N}";
        sb.Append("Content-Type: multipart/related; boundary=\"").Append(related).Append("\"\r\n\r\n");
        sb.Append("--").Append(related).Append("\r\n");
        sb.Append("Content-Type: text/html; charset=UTF-8\r\n");
        sb.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        sb.Append(html).Append("\r\n");

        foreach (var (cid, contentType, base64) in inline)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch (FormatException) { continue; }

            var ext = contentType.Split('/')[1].Split('+')[0];
            var name = $"{cid.Split('.')[0]}.{ext}";
            sb.Append("--").Append(related).Append("\r\n");
            sb.Append("Content-Type: ").Append(contentType).Append("; name=\"").Append(name).Append("\"\r\n");
            sb.Append("Content-Transfer-Encoding: base64\r\n");
            sb.Append("Content-ID: <").Append(cid).Append(">\r\n");
            sb.Append("Content-Disposition: inline; filename=\"").Append(name).Append("\"\r\n\r\n");
            sb.Append(Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks));
            sb.Append("\r\n");
        }

        sb.Append("--").Append(related).Append("--");
        return sb.ToString();
    }

    private async Task<GmailMessageDto> MapMessageAsync(
        Message message,
        CancellationToken cancellationToken)
    {
        var headers = message.Payload?.Headers ?? [];
        var from = Header(headers, "From") ?? "Desconocido";
        var subject = Header(headers, "Subject") ?? "(Sin asunto)";
        var receivedAt = message.InternalDate.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
            : DateTime.UtcNow;

        var plainBody = new StringBuilder();
        var htmlBody = new StringBuilder();
        var attachments = new List<GmailAttachmentDto>();
        var imageParts = new List<EmailInlineImageEmbedder.ImagePart>();

        if (message.Payload is not null)
        {
            await ReadPartAsync(
                message.Id,
                message.Payload,
                plainBody,
                htmlBody,
                attachments,
                imageParts,
                cancellationToken);
        }

        var bodyText = plainBody.Length > 0
            ? plainBody.ToString().Trim()
            : HtmlToText(htmlBody.ToString());

        string? bodyHtml = null;
        if (htmlBody.Length > 0)
        {
            var embedParts = imageParts
                .Concat(attachments.Select(a =>
                    new EmailInlineImageEmbedder.ImagePart(null, a.FileName, a.ContentType, a.Content)))
                .ToList();
            var rawHtml = htmlBody.ToString();
            bodyHtml = EmailInlineImageEmbedder.Embed(rawHtml, embedParts);

            // Si se resolvieron todos los cid:, no listar esas imágenes otra vez como adjuntos.
            if (rawHtml.Contains("cid:", StringComparison.OrdinalIgnoreCase)
                && !bodyHtml.Contains("cid:", StringComparison.OrdinalIgnoreCase))
            {
                attachments.RemoveAll(a =>
                    imageParts.Any(p => ReferenceEquals(p.Content, a.Content)));
            }
        }

        return new GmailMessageDto(
            message.Id,
            message.ThreadId ?? message.Id,
            from,
            SplitAddresses(Header(headers, "To")),
            SplitAddresses(Header(headers, "Cc")),
            Header(headers, "Reply-To"),
            subject,
            bodyText,
            bodyHtml,
            receivedAt,
            Header(headers, "Message-ID") ?? Header(headers, "Message-Id"),
            Header(headers, "In-Reply-To"),
            Header(headers, "References"),
            attachments);
    }

    private async Task ReadPartAsync(
        string messageId,
        MessagePart part,
        StringBuilder plainBody,
        StringBuilder htmlBody,
        List<GmailAttachmentDto> attachments,
        List<EmailInlineImageEmbedder.ImagePart> imageParts,
        CancellationToken cancellationToken)
    {
        if (part.Parts is not null)
        {
            foreach (var child in part.Parts)
            {
                await ReadPartAsync(
                    messageId,
                    child,
                    plainBody,
                    htmlBody,
                    attachments,
                    imageParts,
                    cancellationToken);
            }
        }

        var isTextPart = string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase)
            || (part.MimeType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) ?? false);

        var contentId = NormalizeContentId(Header(part.Headers ?? [], "Content-ID"));
        var contentLocation = NormalizeContentId(Header(part.Headers ?? [], "Content-Location"));
        var disposition = Header(part.Headers ?? [], "Content-Disposition") ?? string.Empty;
        var isInlineDisposition = disposition.Contains("inline", StringComparison.OrdinalIgnoreCase);
        var fileName = part.Filename?.Trim();
        if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(contentLocation))
            fileName = Path.GetFileName(contentLocation);
        var looksLikeImage = EmailInlineImageEmbedder.LooksLikeImage(part.MimeType, fileName);
        // Outlook/ART a menudo manda imagen como octet-stream + Content-ID, sin filename.
        var maybeInlineImage = !isTextPart
            && (!string.IsNullOrWhiteSpace(contentId) || isInlineDisposition);

        byte[]? inlineContent = null;
        if (looksLikeImage || maybeInlineImage)
        {
            inlineContent = await ReadAttachmentContentAsync(messageId, part, cancellationToken);
            if (inlineContent.Length > 0)
            {
                var mime = EmailInlineImageEmbedder.ResolveImageMime(part.MimeType, fileName, inlineContent);
                if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    || looksLikeImage
                    || EmailInlineImageEmbedder.LooksLikeImage(mime, fileName))
                {
                    imageParts.Add(new EmailInlineImageEmbedder.ImagePart(
                        contentId ?? contentLocation,
                        fileName,
                        mime,
                        inlineContent));
                }
                else
                {
                    // No es imagen (p.ej. PDF con Content-ID): no indexar como inline.
                    inlineContent = null;
                }
            }
        }

        var hasFileName = !string.IsNullOrWhiteSpace(fileName);
        if (hasFileName)
        {
            var content = inlineContent ?? await ReadAttachmentContentAsync(messageId, part, cancellationToken);
            if (content.LongLength > _options.MaxAttachmentBytes)
            {
                throw new InvalidOperationException(
                    $"El adjunto '{fileName}' supera el máximo permitido.");
            }

            if (looksLikeImage && inlineContent is null && content.Length > 0)
            {
                var mime = EmailInlineImageEmbedder.ResolveImageMime(part.MimeType, fileName, content);
                imageParts.Add(new EmailInlineImageEmbedder.ImagePart(contentId, fileName, mime, content));
            }

            attachments.Add(new GmailAttachmentDto(
                part.Body?.AttachmentId ?? part.PartId ?? Guid.NewGuid().ToString("N"),
                fileName!,
                EmailInlineImageEmbedder.ResolveImageMime(part.MimeType, fileName, content),
                content.LongLength,
                content));
            return;
        }

        // Imagen inline sin filename: ya está en imageParts; no listar como adjunto.
        if (inlineContent is { Length: > 0 } && maybeInlineImage)
            return;

        var isPlain = string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase);
        var isHtml = string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase);
        if (!isPlain && !isHtml)
            return;

        // Gmail omite body.data en partes grandes y solo envía attachmentId.
        var bytes = await ReadAttachmentContentAsync(messageId, part, cancellationToken);
        if (bytes.Length == 0)
            return;

        var text = Encoding.UTF8.GetString(bytes);
        if (isPlain)
            plainBody.AppendLine(text);
        else
            htmlBody.AppendLine(text);
    }

    private async Task<byte[]> ReadAttachmentContentAsync(
        string messageId,
        MessagePart part,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(part.Body?.Data))
            return DecodeBase64Url(part.Body.Data);

        if (string.IsNullOrWhiteSpace(part.Body?.AttachmentId))
            return [];

        var request = _gmail.Users.Messages.Attachments.Get(
            "me",
            messageId,
            part.Body.AttachmentId);
        var attachment = await request.ExecuteAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(attachment.Data)
            ? []
            : DecodeBase64Url(attachment.Data);
    }

    private static string EncodeHeader(string value)
    {
        if (value.All(c => c < 128))
            return value;
        return $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";
    }

    private static string ExtractEmail(string value)
    {
        try { return new MailAddress(value).Address; }
        catch { return value; }
    }

    private static string ExtractDisplayName(string value)
    {
        try { return new MailAddress(value).DisplayName; }
        catch { return string.Empty; }
    }

    private static IReadOnlyList<string> SplitAddresses(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return [];

        return header
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string? Header(IEnumerable<MessagePartHeader> headers, string name) =>
        headers.FirstOrDefault(
            header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? NormalizeContentId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().Trim('<', '>', '"', '\'');
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Convert.FromBase64String(normalized);
    }

    private static string EncodeBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string HtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutTags = Regex.Replace(html, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(Regex.Replace(withoutTags, @"\s+", " ")).Trim();
    }

    private async Task<(IList<string> AddIds, IList<string> RemoveIds)> ResolveLabelIdsAsync(
        IReadOnlyList<string> addLabelNames,
        IReadOnlyList<string> removeLabelNames,
        bool createMissing,
        CancellationToken cancellationToken)
    {
        var addNames = NormalizeNames(addLabelNames);
        var removeNames = NormalizeNames(removeLabelNames);
        if (addNames.Count == 0 && removeNames.Count == 0)
            return (Array.Empty<string>(), Array.Empty<string>());

        await EnsureLabelsLoadedAsync(cancellationToken);

        var addIds = new List<string>(addNames.Count);
        foreach (var name in addNames)
        {
            var id = await GetOrCreateLabelIdAsync(name, createMissing, cancellationToken);
            if (!string.IsNullOrEmpty(id))
                addIds.Add(id);
        }

        var removeIds = new List<string>(removeNames.Count);
        foreach (var name in removeNames)
        {
            if (_labelIdsByName.TryGetValue(name, out var id))
                removeIds.Add(id);
        }

        return (addIds, removeIds);
    }

    private async Task EnsureLabelsLoadedAsync(CancellationToken cancellationToken)
    {
        if (_labelsLoaded) return;

        await _labelsLock.WaitAsync(cancellationToken);
        try
        {
            if (_labelsLoaded) return;

            var list = await _gmail.Users.Labels.List("me").ExecuteAsync(cancellationToken);
            foreach (var label in list.Labels ?? [])
            {
                if (string.IsNullOrWhiteSpace(label.Name) || string.IsNullOrWhiteSpace(label.Id))
                    continue;
                _labelIdsByName[label.Name] = label.Id;
            }

            _labelsLoaded = true;
        }
        finally
        {
            _labelsLock.Release();
        }
    }

    private async Task<string?> GetOrCreateLabelIdAsync(
        string name,
        bool createIfMissing,
        CancellationToken cancellationToken)
    {
        if (_labelIdsByName.TryGetValue(name, out var existing))
            return existing;

        if (!createIfMissing)
            return null;

        await _labelsLock.WaitAsync(cancellationToken);
        try
        {
            if (_labelIdsByName.TryGetValue(name, out existing))
                return existing;

            var created = await _gmail.Users.Labels.Create(
                new Label
                {
                    Name = name,
                    LabelListVisibility = "labelShow",
                    MessageListVisibility = "show"
                },
                "me").ExecuteAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(created.Id))
                return null;

            _labelIdsByName[name] = created.Id;
            return created.Id;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
        {
            var list = await _gmail.Users.Labels.List("me").ExecuteAsync(cancellationToken);
            foreach (var label in list.Labels ?? [])
            {
                if (string.IsNullOrWhiteSpace(label.Name) || string.IsNullOrWhiteSpace(label.Id))
                    continue;
                _labelIdsByName[label.Name] = label.Id;
            }

            _labelsLoaded = true;
            return _labelIdsByName.TryGetValue(name, out var id) ? id : null;
        }
        finally
        {
            _labelsLock.Release();
        }
    }

    private static List<string> NormalizeNames(IReadOnlyList<string>? names) =>
        (names ?? Array.Empty<string>())
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ValidateOptions(GmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Mailbox) ||
            string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.RefreshToken))
        {
            throw new InvalidOperationException(
                "Gmail está habilitado pero faltan Mailbox, ClientId, ClientSecret o RefreshToken.");
        }
    }

    public void Dispose()
    {
        _labelsLock.Dispose();
        _gmail.Dispose();
    }
}
