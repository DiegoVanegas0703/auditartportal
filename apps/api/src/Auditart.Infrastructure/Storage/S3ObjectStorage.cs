using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Auditart.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Auditart.Infrastructure.Storage;

public class S3ObjectStorage : IObjectStorage
{
    private readonly IConfiguration _configuration;
    private readonly string _bucket;
    private readonly bool _useLocalFallback;
    private readonly string _localRoot;
    private IAmazonS3? _s3;

    public S3ObjectStorage(IConfiguration configuration, IHostEnvironment env)
    {
        _configuration = configuration;
        _bucket = configuration["Aws:S3:Bucket"] ?? "auditart-docs-dev";
        // Demo/GCP: UseLocal=true debe funcionar también en Production (no solo Development).
        _useLocalFallback = string.Equals(
            configuration["Aws:S3:UseLocal"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        var configuredRoot = configuration["Aws:S3:LocalRoot"];
        _localRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : configuredRoot;
    }

    private IAmazonS3 S3 =>
        _s3 ??= new AmazonS3Client(
            RegionEndpoint.GetBySystemName(_configuration["Aws:Region"] ?? "us-east-1"));

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
        await S3.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<ObjectDownload> DownloadAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (_useLocalFallback)
        {
            var root = Path.GetFullPath(_localRoot);
            var fullPath = Path.GetFullPath(
                Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ruta de archivo inválida.");
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("El archivo almacenado no existe.", fullPath);

            return new ObjectDownload(File.OpenRead(fullPath), null);
        }

        var response = await S3.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key
            },
            cancellationToken);
        return new ObjectDownload(response.ResponseStream, response.Headers.ContentType);
    }
}
