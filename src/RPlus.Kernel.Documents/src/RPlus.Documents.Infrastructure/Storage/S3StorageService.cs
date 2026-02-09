using Amazon.S3;
using Amazon.S3.Model;
using RPlus.Documents.Application.Interfaces;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace RPlus.Documents.Infrastructure.Storage;

using Microsoft.Extensions.Logging;

public sealed class S3StorageService(IAmazonS3 s3, S3StorageOptions options, ILogger<S3StorageService> logger) : IStorageService
{
    private static bool _loggedConfig;

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (!_loggedConfig)
        {
            _loggedConfig = true;
            var maskedKey = string.IsNullOrWhiteSpace(options.AccessKey)
                ? "<empty>"
                : $"{options.AccessKey[..Math.Min(4, options.AccessKey.Length)]}***{options.AccessKey[^Math.Min(4, options.AccessKey.Length)..]}";
            logger.LogWarning("Documents S3 config: endpoint={Endpoint} bucket={Bucket} accessKey={AccessKey} region={Region}",
                options.ServiceUrl, options.Bucket, maskedKey, options.Region);
        }

        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = NormalizeKey(key),
            InputStream = content,
            ContentType = contentType
        };

        if (!string.IsNullOrWhiteSpace(options.ServerSideEncryption))
        {
            if (options.ServerSideEncryption.Equals("AES256", StringComparison.OrdinalIgnoreCase))
            {
                request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
            }
            else if (options.ServerSideEncryption.Equals("AWSKMS", StringComparison.OrdinalIgnoreCase))
            {
                request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS;
                if (!string.IsNullOrWhiteSpace(options.KmsKeyId))
                {
                    request.ServerSideEncryptionKeyManagementServiceKeyId = options.KmsKeyId;
                }
            }
        }

        await s3.PutObjectAsync(request, ct);
    }

    public async Task<Stream?> DownloadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectAsync(options.Bucket, NormalizeKey(key), ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(options.Bucket, NormalizeKey(key), ct);
    }

    private string NormalizeKey(string key)
    {
        var trimmed = key.TrimStart('/');
        if (string.IsNullOrWhiteSpace(options.Prefix))
            return trimmed;

        return $"{options.Prefix.TrimEnd('/')}/{trimmed}";
    }
}
