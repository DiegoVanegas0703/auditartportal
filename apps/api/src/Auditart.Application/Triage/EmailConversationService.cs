using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Application.Pacientes;
using Auditart.Domain.Entities;
using Auditart.Domain.Enums;
using Auditart.Application.Sla;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auditart.Application.Triage;

public sealed record PagedRequestsResult(
    IReadOnlyList<EmailRequestSummaryDto> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages,
    int PendingCount,
    int IgnoredCount,
    IReadOnlyList<string> AvailableTags);

public sealed record EmailRequestSummaryDto(
    Guid Id,
    string Subject,
    EmailRequestState State,
    EmailChannel Channel,
    DateTime LastMessageAtUtc,
    int MessageCount,
    int AttachmentCount,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Participants,
    Guid? AuditServiceId,
    string? Preview);

public sealed record EmailRequestDetailDto(
    Guid Id,
    string Subject,
    EmailRequestState State,
    EmailChannel Channel,
    DateTime LastMessageAtUtc,
    int MessageCount,
    int AttachmentCount,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Participants,
    Guid? AuditServiceId,
    IReadOnlyList<EmailMessageDto> Messages,
    IReadOnlyList<EmailAttachmentDto> Attachments,
    ReplyDefaultsDto ReplyDefaults);

public sealed record EmailMessageDto(
    Guid Id,
    Guid ThreadId,
    string? ProviderMessageId,
    EmailDirection Direction,
    EmailMessageStatus Status,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTime OccurredAtUtc,
    string? LastError,
    IReadOnlyList<EmailAttachmentDto> Attachments);

public sealed record EmailAttachmentDto(
    Guid Id,
    Guid MessageId,
    string FileName,
    string ContentType,
    long SizeBytes);

public sealed record ReplyDefaultsDto(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject);

public sealed record ReplyRequestDto(
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    string BodyText,
    string? BodyHtml,
    string? IdempotencyKey,
    IReadOnlyList<Guid>? ExistingAttachmentIds = null);

public sealed record SendEmailRequestDto(
    IReadOnlyList<string> To,
    IReadOnlyList<string>? Cc,
    string Subject,
    string BodyText,
    string? BodyHtml,
    string? IdempotencyKey,
    IReadOnlyList<Guid>? ExistingAttachmentIds = null);

public sealed record UploadedEmailFile(
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed class EmailConversationService
{
    private readonly IAppDbContext _db;
    private readonly GmailIngestionService _ingestion;
    private readonly IGmailChannelRegistry _channels;
    private readonly IObjectStorage _storage;
    private readonly SlaRuleService _slaRules;
    private readonly PacienteService _pacientes;
    private readonly ILogger<EmailConversationService> _logger;

    public EmailConversationService(
        IAppDbContext db,
        GmailIngestionService ingestion,
        IGmailChannelRegistry channels,
        IObjectStorage storage,
        SlaRuleService slaRules,
        PacienteService pacientes,
        ILogger<EmailConversationService> logger)
    {
        _db = db;
        _ingestion = ingestion;
        _channels = channels;
        _storage = storage;
        _slaRules = slaRules;
        _pacientes = pacientes;
        _logger = logger;
    }

    public async Task<PagedRequestsResult> ListAsync(
        int page,
        int pageSize,
        bool ignored,
        string? tag,
        UserRole callerRole,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 50);
        var state = ignored ? EmailRequestState.Ignored : EmailRequestState.Pending;
        var normalizedTag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim().ToLowerInvariant();

        var accessibleChannels = Enum.GetValues<EmailChannel>()
            .Where(channel => PermissionService.CanAccessChannel(callerRole, channel))
            .ToArray();

        var pendingCount = await _db.EmailRequests.CountAsync(
            r => r.State == EmailRequestState.Pending && accessibleChannels.Contains(r.Channel), ct);
        var ignoredCount = await _db.EmailRequests.CountAsync(
            r => r.State == EmailRequestState.Ignored && accessibleChannels.Contains(r.Channel), ct);
        var availableTags = (await _db.EmailRequests
                .Where(r => r.State != EmailRequestState.Closed
                    && r.Tags.Count > 0
                    && accessibleChannels.Contains(r.Channel))
                .Select(r => r.Tags)
                .ToListAsync(ct))
            .SelectMany(t => t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToArray();

        var query = _db.EmailRequests
            .Where(r => r.State == state && accessibleChannels.Contains(r.Channel));
        if (normalizedTag is not null)
            query = query.Where(r => r.Tags.Contains(normalizedTag));

        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await query
            .OrderByDescending(r => r.LastMessageAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new EmailRequestSummaryDto(
                r.Id,
                r.Subject,
                r.State,
                r.Channel,
                r.LastMessageAtUtc,
                r.MessageCount,
                r.AttachmentCount,
                r.Tags,
                r.Participants,
                r.AuditServiceId,
                r.Threads
                    .SelectMany(t => t.Messages)
                    .OrderByDescending(m => m.OccurredAtUtc)
                    .Select(m => m.BodyText)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedRequestsResult(
            items,
            page,
            pageSize,
            total,
            totalPages,
            pendingCount,
            ignoredCount,
            availableTags);
    }

    public async Task<EmailRequestDetailDto?> GetAsync(Guid requestId, CancellationToken ct)
    {
        var request = await _db.EmailRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null) return null;

        var messages = await _db.EmailMessages
            .AsNoTracking()
            .Where(m => m.EmailThread!.EmailRequestId == requestId)
            .OrderBy(m => m.OccurredAtUtc)
            .Select(m => new EmailMessageDto(
                m.Id,
                m.EmailThreadId,
                m.ProviderMessageId,
                m.Direction,
                m.Status,
                m.FromAddress,
                m.ToAddresses,
                m.CcAddresses,
                m.Subject,
                m.BodyText,
                m.BodyHtml,
                m.OccurredAtUtc,
                m.LastError,
                m.Attachments
                    .OrderBy(a => a.FileName)
                    .Select(a => new EmailAttachmentDto(
                        a.Id,
                        a.EmailMessageId,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes))
                    .ToList()))
            .ToListAsync(ct);

        var attachments = messages.SelectMany(m => m.Attachments).ToList();
        return new EmailRequestDetailDto(
            request.Id,
            request.Subject,
            request.State,
            request.Channel,
            request.LastMessageAtUtc,
            request.MessageCount,
            request.AttachmentCount,
            request.Tags,
            request.Participants,
            request.AuditServiceId,
            messages,
            attachments,
            BuildReplyDefaults(messages, request.Subject, ResolveMailboxForRequest(request)));
    }

    public async Task IgnoreAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        var request = await _db.EmailRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento no encontrado.");
        request.Ignore(userId);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid requestId, CancellationToken ct)
    {
        var request = await _db.EmailRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento no encontrado.");
        request.Restore();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> UpdateTagsAsync(
        Guid requestId,
        IEnumerable<string> tags,
        CancellationToken ct)
    {
        var request = await _db.EmailRequests
            .Include(r => r.Threads)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento no encontrado.");

        var previous = request.Tags.ToList();
        request.SetTags(tags);
        await _db.SaveChangesAsync(ct);

        await TrySyncTagsToGmailAsync(request, previous, request.Tags, ct);
        return request.Tags;
    }

    private async Task TrySyncTagsToGmailAsync(
        EmailRequest request,
        IReadOnlyList<string> previous,
        IReadOnlyList<string> next,
        CancellationToken ct)
    {
        var added = next.Except(previous, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = previous.Except(next, StringComparer.OrdinalIgnoreCase).ToList();
        if (added.Count == 0 && removed.Count == 0)
            return;

        var threadIds = request.Threads
            .Select(t => t.ProviderThreadId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (threadIds.Count == 0)
            return;

        try
        {
            var gmail = _channels.GetInbox(request.Channel);
            foreach (var threadId in threadIds)
            {
                await gmail.ModifyThreadLabelsByNameAsync(threadId, added, removed, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudieron sincronizar tags a Gmail para el requerimiento {RequestId}",
                request.Id);
        }
    }

    public async Task MergeAsync(Guid targetRequestId, IReadOnlyList<Guid> sourceRequestIds, CancellationToken ct)
    {
        var target = await _db.EmailRequests
            .Include(r => r.Threads)
            .FirstOrDefaultAsync(r => r.Id == targetRequestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento destino no encontrado.");

        var sources = await _db.EmailRequests
            .Include(r => r.Threads)
            .Where(r => sourceRequestIds.Contains(r.Id) && r.Id != targetRequestId)
            .ToListAsync(ct);

        foreach (var source in sources)
        {
            target.Absorb(source);
            _db.Remove(source);
        }

        await _db.SaveChangesAsync(ct);
        await _ingestion.RefreshRequestSummaryAsync(target.Id, ct);
    }

    public async Task<AuditService> AssignAsync(
        Guid requestId,
        Guid operadorId,
        AuditQueue queue,
        Guid assignedByUserId,
        string triageNote,
        string? paciente,
        string? dni,
        string? art,
        string? numeroSiniestro,
        string? telefonoPaciente,
        string? emailPaciente,
        ServiceType? tipoServicio,
        string? especialidad,
        UrgencyLevel? urgency,
        Stream? attachmentContent,
        string? attachmentFileName,
        string? attachmentContentType,
        long? attachmentSizeBytes,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(triageNote))
            throw new InvalidOperationException("La nota de triage es obligatoria al derivar.");
        if (string.IsNullOrWhiteSpace(art))
            throw new InvalidOperationException("La ART (aseguradora) es obligatoria al derivar.");
        if (string.IsNullOrWhiteSpace(numeroSiniestro))
            throw new InvalidOperationException("El número de siniestro es obligatorio al derivar.");
        if (string.IsNullOrWhiteSpace(telefonoPaciente))
            throw new InvalidOperationException("El teléfono del paciente es obligatorio al derivar.");
        if (string.IsNullOrWhiteSpace(emailPaciente))
            throw new InvalidOperationException("El correo del paciente es obligatorio al derivar.");

        var request = await _db.EmailRequests
            .Include(r => r.Threads)
            .ThenInclude(t => t.Messages)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento no encontrado.");

        if (request.State == EmailRequestState.Assigned)
            throw new InvalidOperationException("El requerimiento ya fue derivado.");
        if (request.State == EmailRequestState.Ignored)
            throw new InvalidOperationException("Restaurá el requerimiento antes de derivarlo.");

        var operador = await _db.Users.FirstOrDefaultAsync(u => u.Id == operadorId && u.IsActive, ct)
            ?? throw new InvalidOperationException("Operador inválido.");

        var nextNumero = await _db.AuditServices.AnyAsync(ct)
            ? await _db.AuditServices.MaxAsync(s => s.Numero, ct) + 1
            : 1001;

        var lastMessage = request.Threads
            .SelectMany(t => t.Messages)
            .OrderByDescending(m => m.OccurredAtUtc)
            .FirstOrDefault();

        var defaultUrgency = (lastMessage?.Subject ?? request.Subject)
            .Contains("URGENT", StringComparison.OrdinalIgnoreCase)
            ? UrgencyLevel.Critica
            : UrgencyLevel.Alta;

        // Compat: primer IncomingEmail ligado
        var firstIncomingId = await _db.EmailMessages
            .Where(m => m.EmailThread!.EmailRequestId == requestId && m.IncomingEmailId != null)
            .OrderBy(m => m.OccurredAtUtc)
            .Select(m => m.IncomingEmailId)
            .FirstOrDefaultAsync(ct);

        var notas = $"Derivado desde requerimiento: {request.Subject}\n\nNota de triage:\n{triageNote.Trim()}";

        var service = AuditService.CreateFromTriage(
            nextNumero,
            string.IsNullOrWhiteSpace(paciente) ? "Sin identificar" : paciente.Trim(),
            art.Trim(),
            tipoServicio ?? ServiceType.Consultorio,
            queue,
            operador.Id,
            urgency ?? defaultUrgency,
            firstIncomingId,
            especialidad: string.IsNullOrWhiteSpace(especialidad) ? null : especialidad.Trim(),
            dni: string.IsNullOrWhiteSpace(dni) ? null : dni.Trim(),
            notas: notas,
            emailRequestId: request.Id,
            numeroSiniestro: numeroSiniestro.Trim(),
            telefonoPaciente: telefonoPaciente.Trim(),
            emailPaciente: emailPaciente.Trim());

        var rojoDeadline = await _slaRules.ComputeDeadlineAsync(
            queue,
            AuditStatus.Rojo,
            DateTime.UtcNow,
            ct);
        service.ApplySlaDeadline(rojoDeadline);
        await _pacientes.LinkServiceAsync(service, ct);

        _db.Add(service);
        request.MarkAssigned(service.Id, assignedByUserId);

        if (attachmentContent is not null && !string.IsNullOrWhiteSpace(attachmentFileName))
        {
            var key = await _storage.UploadAsync(
                attachmentContent,
                attachmentFileName,
                attachmentContentType ?? "application/octet-stream",
                $"triage/{requestId}",
                ct);
            _db.Add(ServiceAttachment.Create(
                attachmentFileName,
                attachmentContentType ?? "application/octet-stream",
                attachmentSizeBytes ?? 0,
                key,
                s3Bucket: null,
                auditServiceId: service.Id));
        }

        var incomingIds = await _db.EmailMessages
            .Where(m => m.EmailThread!.EmailRequestId == requestId && m.IncomingEmailId != null)
            .Select(m => m.IncomingEmailId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var attachments = await _db.ServiceAttachments
            .Where(a => a.IncomingEmailId != null && incomingIds.Contains(a.IncomingEmailId.Value))
            .ToListAsync(ct);
        foreach (var attachment in attachments)
            attachment.LinkToService(service.Id);

        foreach (var incomingId in incomingIds)
        {
            var email = await _db.IncomingEmails.FirstOrDefaultAsync(e => e.Id == incomingId, ct);
            email?.MarkAssigned(assignedByUserId, service.Id);
        }

        await _db.SaveChangesAsync(ct);
        return service;
    }

    public async Task<EmailMessageDto> ReplyAsync(
        Guid requestId,
        Guid userId,
        ReplyRequestDto request,
        IReadOnlyList<UploadedEmailFile>? uploads,
        CancellationToken ct)
    {
        if (request.To is null || request.To.Count == 0)
            throw new InvalidOperationException("Indicá al menos un destinatario.");
        if (string.IsNullOrWhiteSpace(request.BodyText) && string.IsNullOrWhiteSpace(request.BodyHtml))
            throw new InvalidOperationException("El cuerpo de la respuesta es obligatorio.");

        uploads ??= [];
        ValidateUploads(uploads);

        var idempotency = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        var existing = await _db.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotency, ct);
        if (existing is not null)
        {
            return (await GetMessageDtoAsync(existing.Id, ct))!;
        }

        var emailRequest = await _db.EmailRequests
            .Include(r => r.Threads)
            .ThenInclude(t => t.Messages)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new KeyNotFoundException("Requerimiento no encontrado.");

        if (emailRequest.State == EmailRequestState.Ignored)
            throw new InvalidOperationException("No se puede responder un requerimiento ignorado.");

        var serviceId = emailRequest.AuditServiceId;
        if ((uploads.Count > 0 || (request.ExistingAttachmentIds?.Count ?? 0) > 0) && serviceId is null)
            throw new InvalidOperationException(
                "Para adjuntar archivos al responder, el requerimiento debe estar derivado a un caso.");

        var mailbox = ResolveMailboxForRequest(emailRequest);

        var thread = emailRequest.Threads
            .OrderByDescending(t => t.LastMessageAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("El requerimiento no tiene hilos.");

        var lastInbound = thread.Messages
            .Where(m => m.Direction == EmailDirection.Inbound)
            .OrderByDescending(m => m.OccurredAtUtc)
            .FirstOrDefault()
            ?? thread.Messages.OrderByDescending(m => m.OccurredAtUtc).First();

        var subject = lastInbound.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? lastInbound.Subject
            : $"Re: {lastInbound.Subject}";

        var references = string.Join(
            ' ',
            new[] { lastInbound.ReferencesHeader, lastInbound.InternetMessageId }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        IReadOnlyList<ResolvedAttachmentSource> selectedExisting = [];
        if (serviceId is Guid sid)
        {
            selectedExisting = await ResolveExistingAttachmentsAsync(
                sid,
                emailRequest.Id,
                request.ExistingAttachmentIds ?? [],
                ct);

            const long maxGmailTotalBytes = 23L * 1024 * 1024;
            var totalBytes = selectedExisting.Sum(a => a.SizeBytes) + uploads.Sum(u => u.SizeBytes);
            if (totalBytes > maxGmailTotalBytes)
                throw new InvalidOperationException(
                    "Los adjuntos superan ~23 MB en total (límite de Gmail para un mensaje).");
        }

        var outbound = EmailMessage.CreateOutboundPending(
            thread.Id,
            mailbox,
            request.To,
            request.Cc,
            subject,
            request.BodyText ?? string.Empty,
            request.BodyHtml,
            lastInbound.InternetMessageId,
            string.IsNullOrWhiteSpace(references) ? lastInbound.InternetMessageId : references,
            userId,
            idempotency);
        _db.Add(outbound);
        _db.Add(EmailSendOutbox.Create(outbound.Id));

        if (serviceId is Guid linkedServiceId)
            await AttachOutboundFilesAsync(linkedServiceId, outbound.Id, selectedExisting, uploads, ct);

        await _db.SaveChangesAsync(ct);
        await _ingestion.RefreshRequestSummaryAsync(requestId, ct);

        return (await GetMessageDtoAsync(outbound.Id, ct))!;
    }

    public async Task<EmailMessageDto> SendNewEmailAsync(
        Guid serviceId,
        Guid userId,
        SendEmailRequestDto request,
        IReadOnlyList<UploadedEmailFile>? uploads,
        CancellationToken ct)
    {
        if (request.To is null || request.To.Count == 0)
            throw new InvalidOperationException("Indicá al menos un destinatario.");
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("El asunto es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.BodyText) && string.IsNullOrWhiteSpace(request.BodyHtml))
            throw new InvalidOperationException("El cuerpo del mensaje es obligatorio.");

        const long maxGmailTotalBytes = 23L * 1024 * 1024;
        uploads ??= [];
        ValidateUploads(uploads);

        var service = await _db.AuditServices.FirstOrDefaultAsync(s => s.Id == serviceId, ct)
            ?? throw new KeyNotFoundException("Servicio no encontrado.");

        var emailRequest = await _db.EmailRequests
            .Include(r => r.Threads)
            .FirstOrDefaultAsync(r => r.AuditServiceId == serviceId || r.Id == service.EmailRequestId, ct);

        var channel = emailRequest?.Channel ?? EmailChannel.General;
        var mailbox = _channels.GetMailbox(channel);
        var idempotency = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        var existing = await _db.EmailMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdempotencyKey == idempotency, ct);
        if (existing is not null)
            return (await GetMessageDtoAsync(existing.Id, ct))!;

        var selectedExisting = await ResolveExistingAttachmentsAsync(
            serviceId,
            emailRequest?.Id,
            request.ExistingAttachmentIds ?? [],
            ct);

        var totalBytes = selectedExisting.Sum(a => a.SizeBytes) + uploads.Sum(u => u.SizeBytes);
        if (totalBytes > maxGmailTotalBytes)
            throw new InvalidOperationException(
                "Los adjuntos superan ~23 MB en total (límite de Gmail para un mensaje).");

        EmailThread thread;
        if (emailRequest is null)
        {
            emailRequest = EmailRequest.Create(request.Subject, DateTime.UtcNow, channel);
            emailRequest.MarkAssigned(service.Id, userId);
            _db.Add(emailRequest);

            thread = EmailThread.Create(
                emailRequest.Id,
                mailbox,
                $"new-{Guid.NewGuid():N}",
                request.Subject,
                DateTime.UtcNow);
            _db.Add(thread);
        }
        else
        {
            thread = emailRequest.Threads.OrderByDescending(t => t.LastMessageAtUtc).FirstOrDefault();
            if (thread is null)
            {
                thread = EmailThread.Create(
                    emailRequest.Id,
                    mailbox,
                    $"new-{Guid.NewGuid():N}",
                    request.Subject,
                    DateTime.UtcNow);
                _db.Add(thread);
            }
        }

        var outbound = EmailMessage.CreateOutboundPending(
            thread.Id,
            mailbox,
            request.To,
            request.Cc,
            request.Subject,
            request.BodyText ?? string.Empty,
            request.BodyHtml,
            inReplyTo: null,
            references: null,
            userId,
            idempotency);
        _db.Add(outbound);
        _db.Add(EmailSendOutbox.Create(outbound.Id));

        await AttachOutboundFilesAsync(serviceId, outbound.Id, selectedExisting, uploads, ct);

        await _db.SaveChangesAsync(ct);
        await _ingestion.RefreshRequestSummaryAsync(emailRequest.Id, ct);

        return (await GetMessageDtoAsync(outbound.Id, ct))!;
    }

    private static void ValidateUploads(IReadOnlyList<UploadedEmailFile> uploads)
    {
        const long maxFileBytes = 50L * 1024 * 1024;
        foreach (var upload in uploads)
        {
            if (upload.SizeBytes <= 0)
                throw new InvalidOperationException($"El archivo '{upload.FileName}' está vacío.");
            if (upload.SizeBytes > maxFileBytes)
                throw new InvalidOperationException(
                    $"'{upload.FileName}' supera el máximo de 50 MB.");
            if (!IsAllowedAttachment(upload.FileName, upload.ContentType))
                throw new InvalidOperationException(
                    $"Tipo no permitido: '{upload.FileName}'. Usá PDF, Word, Excel o imagen.");
        }
    }

    private async Task AttachOutboundFilesAsync(
        Guid serviceId,
        Guid outboundMessageId,
        IReadOnlyList<ResolvedAttachmentSource> selectedExisting,
        IReadOnlyList<UploadedEmailFile> uploads,
        CancellationToken ct)
    {
        foreach (var source in selectedExisting)
        {
            var serviceAttachmentId = await EnsureCaseAttachmentAsync(serviceId, source, ct);
            _db.Add(EmailAttachment.Create(
                outboundMessageId,
                source.FileName,
                source.ContentType,
                source.SizeBytes,
                source.S3Key,
                source.S3Bucket,
                serviceAttachmentId: serviceAttachmentId));
        }

        foreach (var upload in uploads)
        {
            var key = await _storage.UploadAsync(
                upload.Content,
                upload.FileName,
                upload.ContentType,
                $"outbound/{serviceId:N}/{DateTime.UtcNow:yyyy/MM/dd}",
                ct);

            var serviceAttachment = ServiceAttachment.Create(
                upload.FileName,
                upload.ContentType,
                upload.SizeBytes,
                key,
                s3Bucket: null,
                auditServiceId: serviceId);
            _db.Add(serviceAttachment);

            _db.Add(EmailAttachment.Create(
                outboundMessageId,
                upload.FileName,
                upload.ContentType,
                upload.SizeBytes,
                key,
                serviceAttachmentId: serviceAttachment.Id));
        }
    }

    private async Task<Guid> EnsureCaseAttachmentAsync(
        Guid serviceId,
        ResolvedAttachmentSource source,
        CancellationToken ct)
    {
        if (source.ServiceAttachmentId is Guid existingId)
        {
            var existing = await _db.ServiceAttachments
                .FirstOrDefaultAsync(a => a.Id == existingId, ct);
            if (existing is not null)
            {
                if (existing.AuditServiceId != serviceId)
                    existing.LinkToService(serviceId);
                return existing.Id;
            }
        }

        var created = ServiceAttachment.Create(
            source.FileName,
            source.ContentType,
            source.SizeBytes,
            source.S3Key,
            source.S3Bucket,
            auditServiceId: serviceId);
        _db.Add(created);
        return created.Id;
    }

    private async Task<IReadOnlyList<ResolvedAttachmentSource>> ResolveExistingAttachmentsAsync(
        Guid serviceId,
        Guid? emailRequestId,
        IReadOnlyList<Guid> attachmentIds,
        CancellationToken ct)
    {
        if (attachmentIds.Count == 0)
            return [];

        var distinctIds = attachmentIds.Distinct().ToList();
        var sources = new List<ResolvedAttachmentSource>();

        var emailAttachments = await (
            from a in _db.EmailAttachments.AsNoTracking()
            join m in _db.EmailMessages.AsNoTracking() on a.EmailMessageId equals m.Id
            join t in _db.EmailThreads.AsNoTracking() on m.EmailThreadId equals t.Id
            where distinctIds.Contains(a.Id) &&
                  (emailRequestId != null && t.EmailRequestId == emailRequestId.Value)
            select new ResolvedAttachmentSource(
                a.Id,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.S3Key,
                a.S3Bucket,
                a.ServiceAttachmentId)).ToListAsync(ct);
        sources.AddRange(emailAttachments);

        var foundIds = sources.Select(s => s.Id).ToHashSet();
        var missing = distinctIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            var serviceAttachments = await _db.ServiceAttachments
                .AsNoTracking()
                .Where(a => missing.Contains(a.Id) && a.AuditServiceId == serviceId)
                .Select(a => new ResolvedAttachmentSource(
                    a.Id,
                    a.FileName,
                    a.ContentType,
                    a.SizeBytes,
                    a.S3Key,
                    a.S3Bucket,
                    a.Id))
                .ToListAsync(ct);
            sources.AddRange(serviceAttachments);
            foundIds = sources.Select(s => s.Id).ToHashSet();
        }

        var stillMissing = distinctIds.Where(id => !foundIds.Contains(id)).ToList();
        if (stillMissing.Count > 0)
            throw new InvalidOperationException("Uno o más adjuntos seleccionados no pertenecen a este caso.");

        return sources;
    }

    private static bool IsAllowedAttachment(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };
        if (allowedExt.Contains(ext))
            return true;

        var ct = (contentType ?? string.Empty).ToLowerInvariant();
        return ct.StartsWith("image/") ||
               ct is "application/pdf"
                   or "application/msword"
                   or "application/vnd.ms-excel"
                   or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                   or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    private sealed record ResolvedAttachmentSource(
        Guid Id,
        string FileName,
        string ContentType,
        long SizeBytes,
        string S3Key,
        string? S3Bucket,
        Guid? ServiceAttachmentId);

    public async Task<EmailRequestDetailDto?> GetByServiceAsync(Guid serviceId, CancellationToken ct)
    {
        var requestId = await _db.EmailRequests
            .Where(r => r.AuditServiceId == serviceId)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct)
            ?? await _db.AuditServices
                .Where(s => s.Id == serviceId)
                .Select(s => s.EmailRequestId)
                .FirstOrDefaultAsync(ct);

        if (requestId is null) return null;
        var detail = await GetAsync(requestId.Value, ct);
        if (detail is null) return null;

        var serviceFiles = await _db.ServiceAttachments
            .AsNoTracking()
            .Where(a => a.AuditServiceId == serviceId)
            .OrderBy(a => a.FileName)
            .Select(a => new EmailAttachmentDto(
                a.Id,
                Guid.Empty,
                a.FileName,
                a.ContentType,
                a.SizeBytes))
            .ToListAsync(ct);

        if (serviceFiles.Count == 0)
            return detail;

        var merged = detail.Attachments
            .Concat(serviceFiles)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .OrderBy(a => a.FileName)
            .ToList();

        return detail with { Attachments = merged };
    }

    private async Task<EmailMessageDto?> GetMessageDtoAsync(Guid messageId, CancellationToken ct)
    {
        return await _db.EmailMessages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new EmailMessageDto(
                m.Id,
                m.EmailThreadId,
                m.ProviderMessageId,
                m.Direction,
                m.Status,
                m.FromAddress,
                m.ToAddresses,
                m.CcAddresses,
                m.Subject,
                m.BodyText,
                m.BodyHtml,
                m.OccurredAtUtc,
                m.LastError,
                m.Attachments
                    .OrderBy(a => a.FileName)
                    .Select(a => new EmailAttachmentDto(
                        a.Id,
                        a.EmailMessageId,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
    }

    private string ResolveMailboxForRequest(EmailRequest request)
    {
        var threadMailbox = request.Threads
            .OrderByDescending(t => t.LastMessageAtUtc)
            .Select(t => t.Mailbox)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(threadMailbox))
            return threadMailbox.Trim().ToLowerInvariant();

        return _channels.GetMailbox(request.Channel);
    }

    private ReplyDefaultsDto BuildReplyDefaults(
        IReadOnlyList<EmailMessageDto> messages,
        string subject,
        string mailbox)
    {
        var lastInbound = messages
            .Where(m => m.Direction == EmailDirection.Inbound)
            .OrderByDescending(m => m.OccurredAtUtc)
            .FirstOrDefault()
            ?? messages.OrderByDescending(m => m.OccurredAtUtc).FirstOrDefault();

        if (lastInbound is null)
            return new ReplyDefaultsDto([], [], subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : $"Re: {subject}");

        bool IsSelf(string address) =>
            ExtractEmail(address).Equals(mailbox, StringComparison.OrdinalIgnoreCase);

        var to = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!IsSelf(lastInbound.From))
            to.Add(lastInbound.From);
        foreach (var address in lastInbound.To.Where(a => !IsSelf(a)))
            to.Add(address);
        if (to.Count == 0)
            to.Add(lastInbound.From);

        var cc = lastInbound.Cc
            .Where(a => !to.Contains(a) && !IsSelf(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var replySubject = lastInbound.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? lastInbound.Subject
            : $"Re: {lastInbound.Subject}";

        return new ReplyDefaultsDto(to.ToArray(), cc, replySubject);
    }

    private static string ExtractEmail(string value)
    {
        var start = value.IndexOf('<');
        var end = value.IndexOf('>');
        if (start >= 0 && end > start)
            return value[(start + 1)..end].Trim().ToLowerInvariant();
        return value.Trim().ToLowerInvariant();
    }
}
