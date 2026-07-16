using Auditart.Domain.Common;

namespace Auditart.Domain.Entities;

public class ServiceAttachment : Entity
{
    public Guid? AuditServiceId { get; private set; }
    public AuditService? AuditService { get; private set; }
    public Guid? IncomingEmailId { get; private set; }
    public IncomingEmail? IncomingEmail { get; private set; }

    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string S3Key { get; private set; } = string.Empty;
    public string? S3Bucket { get; private set; }

    private ServiceAttachment() { }

    public static ServiceAttachment Create(
        string fileName,
        string contentType,
        long sizeBytes,
        string s3Key,
        string? s3Bucket,
        Guid? auditServiceId = null,
        Guid? incomingEmailId = null)
    {
        return new ServiceAttachment
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            S3Key = s3Key,
            S3Bucket = s3Bucket,
            AuditServiceId = auditServiceId,
            IncomingEmailId = incomingEmailId
        };
    }

    public void LinkToService(Guid auditServiceId)
    {
        AuditServiceId = auditServiceId;
        Touch();
    }
}
