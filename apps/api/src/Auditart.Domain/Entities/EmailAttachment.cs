using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class EmailAttachment : Entity
{
    public Guid EmailMessageId { get; private set; }
    public EmailMessage? EmailMessage { get; private set; }

    public string? ProviderAttachmentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string S3Key { get; private set; } = string.Empty;
    public string? S3Bucket { get; private set; }
    public string? Sha256 { get; private set; }

    /// <summary>Puente temporal hacia ServiceAttachment durante la transición.</summary>
    public Guid? ServiceAttachmentId { get; private set; }

    private EmailAttachment() { }

    public static EmailAttachment Create(
        Guid emailMessageId,
        string fileName,
        string contentType,
        long sizeBytes,
        string s3Key,
        string? s3Bucket = null,
        string? providerAttachmentId = null,
        string? sha256 = null,
        Guid? serviceAttachmentId = null)
    {
        return new EmailAttachment
        {
            EmailMessageId = emailMessageId,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,
            SizeBytes = sizeBytes,
            S3Key = s3Key,
            S3Bucket = s3Bucket,
            ProviderAttachmentId = providerAttachmentId,
            Sha256 = sha256,
            ServiceAttachmentId = serviceAttachmentId
        };
    }
}
