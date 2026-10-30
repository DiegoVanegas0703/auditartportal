using System.Security.Claims;
using Auditart.Application.Abstractions;
using Auditart.Application.Auth;
using Auditart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auditart.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/attachments")]
public sealed class AttachmentsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IObjectStorage _storage;

    public AttachmentsController(IAppDbContext db, IObjectStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid attachmentId, CancellationToken ct)
    {
        var role = GetRole();
        var userId = GetUserId();

        var serviceAttachment = await _db.ServiceAttachments
            .Include(item => item.AuditService)
            .Include(item => item.IncomingEmail)
            .FirstOrDefaultAsync(item => item.Id == attachmentId, ct);
        if (serviceAttachment is not null)
        {
            if (!await CanDownloadServiceAttachmentAsync(serviceAttachment, role, userId, ct))
                return NotFound();
            return await StreamFileAsync(
                serviceAttachment.S3Key,
                serviceAttachment.ContentType,
                serviceAttachment.FileName,
                ct);
        }

        var emailAttachment = await _db.EmailAttachments
            .Include(item => item.EmailMessage!)
                .ThenInclude(m => m.EmailThread!)
                    .ThenInclude(t => t.EmailRequest!)
                        .ThenInclude(r => r.AuditService)
            .FirstOrDefaultAsync(item => item.Id == attachmentId, ct);
        if (emailAttachment is null)
            return NotFound();

        if (!CanDownloadEmailAttachment(emailAttachment, role, userId))
            return NotFound();

        return await StreamFileAsync(
            emailAttachment.S3Key,
            emailAttachment.ContentType,
            emailAttachment.FileName,
            ct);
    }

    private async Task<bool> CanDownloadServiceAttachmentAsync(
        Domain.Entities.ServiceAttachment attachment,
        UserRole role,
        Guid userId,
        CancellationToken ct)
    {
        if (attachment.AuditService is not null)
            return PermissionService.CanViewService(
                role,
                userId,
                attachment.AuditService.Queue,
                attachment.AuditService.Status,
                attachment.AuditService.OperadorId);

        if (attachment.AuditServiceId is Guid serviceId)
        {
            var service = await _db.AuditServices.AsNoTracking()
                .Select(s => new { s.Id, s.Queue, s.Status, s.OperadorId })
                .FirstOrDefaultAsync(s => s.Id == serviceId, ct);
            if (service is not null)
                return PermissionService.CanViewService(role, userId, service.Queue, service.Status, service.OperadorId);
        }

        var resultingId = attachment.IncomingEmail?.ResultingServiceId;
        if (resultingId is Guid linkedId)
        {
            var service = await _db.AuditServices.AsNoTracking()
                .Select(s => new { s.Id, s.Queue, s.Status, s.OperadorId })
                .FirstOrDefaultAsync(s => s.Id == linkedId, ct);
            if (service is not null)
                return PermissionService.CanViewService(role, userId, service.Queue, service.Status, service.OperadorId);
        }

        // Adjunto de triage aún no asignado a un caso.
        var suggested = attachment.IncomingEmail?.SuggestedQueue;
        if (suggested == AuditQueue.Cronicos)
            return PermissionService.CanAccessChannel(role, EmailChannel.Cronicos)
                && (PermissionService.CanTriage(role) || role == UserRole.Cronicos);
        return PermissionService.CanTriage(role);
    }

    private static bool CanDownloadEmailAttachment(
        Domain.Entities.EmailAttachment attachment,
        UserRole role,
        Guid userId)
    {
        var request = attachment.EmailMessage?.EmailThread?.EmailRequest;
        if (request is null)
            return PermissionService.CanTriage(role);

        if (request.AuditService is not null
            && PermissionService.CanViewService(
                role,
                userId,
                request.AuditService.Queue,
                request.AuditService.Status,
                request.AuditService.OperadorId))
            return true;

        return PermissionService.CanAccessChannel(role, request.Channel)
            && (PermissionService.CanTriage(role) || role == UserRole.Cronicos);
    }

    private async Task<IActionResult> StreamFileAsync(
        string key,
        string contentType,
        string fileName,
        CancellationToken ct)
    {
        try
        {
            var download = await _storage.DownloadAsync(key, ct);
            return File(
                download.Content,
                download.ContentType ?? contentType,
                fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private UserRole GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : UserRole.Operador;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
