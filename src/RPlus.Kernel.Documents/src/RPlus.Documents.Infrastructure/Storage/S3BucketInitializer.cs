using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Documents.Infrastructure.Storage;

public sealed class S3BucketInitializer(IAmazonS3 s3, S3StorageOptions options, ILogger<S3BucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.AutoCreateBucket)
            return;

        try
        {
            var maskedKey = string.IsNullOrWhiteSpace(options.AccessKey)
                ? "<empty>"
                : $"{options.AccessKey[..Math.Min(4, options.AccessKey.Length)]}***{options.AccessKey[^Math.Min(4, options.AccessKey.Length)..]}";
            logger.LogWarning("Documents S3 init: endpoint={Endpoint} bucket={Bucket} accessKey={AccessKey} region={Region}",
                options.ServiceUrl, options.Bucket, maskedKey, options.Region);
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Bucket);
            if (!exists)
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = options.Bucket }, cancellationToken);
            }
        }
        catch (System.Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure S3 bucket {Bucket}", options.Bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
