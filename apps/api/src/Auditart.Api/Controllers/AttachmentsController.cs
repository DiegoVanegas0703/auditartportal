using Auditart.Application.Abstractions;
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
        var serviceAttachment = await _db.ServiceAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId, ct);
        if (serviceAttachment is not null)
            return await StreamFileAsync(serviceAttachment.S3Key, serviceAttachment.ContentType, serviceAttachment.FileName, ct);

        var emailAttachment = await _db.EmailAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId, ct);
        if (emailAttachment is null)
            return NotFound();

        return await StreamFileAsync(emailAttachment.S3Key, emailAttachment.ContentType, emailAttachment.FileName, ct);
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
}
