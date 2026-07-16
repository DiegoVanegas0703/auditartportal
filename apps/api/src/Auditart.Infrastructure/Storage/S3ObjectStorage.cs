using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Auditart.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Auditart.Infrastructure.Storage;

public class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly bool _useLocalFallback;
    private readonly string _localRoot;

    public S3ObjectStorage(IConfiguration configuration, IHostEnvironment env)
    {
        _bucket = configuration["Aws:S3:Bucket"] ?? "auditart-docs-dev";
        var region = configuration["Aws:Region"] ?? "us-east-1";
        _useLocalFallback = env.IsDevelopment() &&
            string.Equals(configuration["Aws:S3:UseLocal"], "true", StringComparison.OrdinalIgnoreCase);

        _localRoot = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        _s3 = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<string> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var key = $"{folder.Trim('/')}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}_{safeName}";

        if (_useLocalFallback)
        {
            var fullPath = Path.Combine(_localRoot, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var fs = File.Create(fullPath);
            await content.CopyToAsync(fs, cancellationToken);
            return key;
        }

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };
        await _s3.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<string> GetPresignedUrlAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_useLocalFallback)
            return $"/api/files/local/{Uri.EscapeDataString(key)}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(ttl),
            Verb = HttpVerb.GET
        };
        return await Task.FromResult(_s3.GetPreSignedURL(request));
    }
}
