using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Amazon.Runtime;
using RPlus.Documents.Application.Interfaces;
using RPlus.Documents.Infrastructure.Antivirus;
using RPlus.Documents.Infrastructure.Encryption;
using RPlus.Documents.Infrastructure.Persistence;
using RPlus.Documents.Infrastructure.Storage;

namespace RPlus.Documents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings__DefaultConnection"]
            ?? configuration["DOCUMENTS_DB_CONNECTION"]
            ?? "Host=rplus-kernel-db;Database=documents;Username=postgres;Password=postgres";

        services.AddDbContext<DocumentsDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "documents");
                })
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IDocumentsDbContext>(sp => sp.GetRequiredService<DocumentsDbContext>());

        var s3Options = configuration.GetSection(S3StorageOptions.SectionName).Get<S3StorageOptions>() ?? new S3StorageOptions();
        if (string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
        {
            s3Options.ServiceUrl = configuration["DOCUMENTS_S3_ENDPOINT"];
        }

        if (string.IsNullOrWhiteSpace(s3Options.Region))
        {
            s3Options.Region = configuration["DOCUMENTS_S3_REGION"];
        }
        if (string.IsNullOrWhiteSpace(s3Options.AccessKey))
        {
            s3Options.AccessKey = configuration["DOCUMENTS_S3_ACCESS_KEY"];
        }

        if (string.IsNullOrWhiteSpace(s3Options.SecretKey))
        {
            s3Options.SecretKey = configuration["DOCUMENTS_S3_SECRET_KEY"];
        }
        s3Options.Bucket = configuration["DOCUMENTS_S3_BUCKET"] ?? s3Options.Bucket;
        s3Options.Prefix = configuration["DOCUMENTS_S3_PREFIX"] ?? s3Options.Prefix;
        s3Options.ServerSideEncryption = configuration["DOCUMENTS_S3_SSE"] ?? s3Options.ServerSideEncryption;
        s3Options.KmsKeyId = configuration["DOCUMENTS_S3_KMS_KEY"] ?? s3Options.KmsKeyId;

        var disableSseEnv = configuration["DOCUMENTS_S3_DISABLE_SSE"];
        var disableSse = !string.IsNullOrWhiteSpace(disableSseEnv) &&
                         (disableSseEnv.Equals("1", StringComparison.OrdinalIgnoreCase)
                          || disableSseEnv.Equals("true", StringComparison.OrdinalIgnoreCase)
                          || disableSseEnv.Equals("yes", StringComparison.OrdinalIgnoreCase));

        if (disableSse)
        {
            s3Options.ServerSideEncryption = string.Empty;
            s3Options.KmsKeyId = null;
        }
        else if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
        {
            // MinIO without KMS rejects SSE; disable SSE for local S3-compatible endpoints by default.
            if (s3Options.ServerSideEncryption.Equals("AES256", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(s3Options.KmsKeyId))
            {
                s3Options.ServerSideEncryption = string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(s3Options.ServiceUrl) && string.IsNullOrWhiteSpace(s3Options.Region))
        {
            s3Options.Region = "us-east-1";
        }

        services.AddSingleton(s3Options);

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config
            {
                ForcePathStyle = s3Options.UsePathStyle,
                SignatureVersion = "4",
                DisableHostPrefixInjection = true
            };

            if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            {
                config.ServiceURL = s3Options.ServiceUrl;
                config.UseHttp = s3Options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                config.AuthenticationRegion = string.IsNullOrWhiteSpace(s3Options.Region)
                    ? "us-east-1"
                    : s3Options.Region;
            }
            else if (!string.IsNullOrWhiteSpace(s3Options.Region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Options.Region);
            }

            if (!string.IsNullOrWhiteSpace(s3Options.AccessKey) &&
                !string.IsNullOrWhiteSpace(s3Options.SecretKey))
            {
                var creds = new BasicAWSCredentials(s3Options.AccessKey, s3Options.SecretKey);
                return new AmazonS3Client(creds, config);
            }

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddHostedService<S3BucketInitializer>();

        var encryptionOptions = configuration.GetSection(FieldEncryptionOptions.SectionName).Get<FieldEncryptionOptions>()
                               ?? new FieldEncryptionOptions { MasterKey = configuration["DOCUMENTS_MASTER_KEY"] };
        services.AddSingleton(encryptionOptions);
        services.AddSingleton<IFieldEncryptor, AesGcmFieldEncryptor>();

        var avOptions = configuration.GetSection(AntivirusOptions.SectionName).Get<AntivirusOptions>()
                        ?? new AntivirusOptions();
        services.AddSingleton(avOptions);
        services.AddSingleton<IAntivirusScanner, ClamAvScanner>();

        return services;
    }
}
