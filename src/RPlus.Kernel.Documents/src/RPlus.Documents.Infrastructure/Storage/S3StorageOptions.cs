namespace RPlus.Documents.Infrastructure.Storage;

public sealed class S3StorageOptions
{
    public const string SectionName = "Documents:Storage:S3";

    public string? ServiceUrl { get; set; }
    public string? Region { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string Bucket { get; set; } = "rplus-documents";
    public bool UsePathStyle { get; set; } = true;
    public bool AutoCreateBucket { get; set; } = true;
    public string Prefix { get; set; } = string.Empty;
    public string ServerSideEncryption { get; set; } = "AES256";
    public string? KmsKeyId { get; set; }
}
