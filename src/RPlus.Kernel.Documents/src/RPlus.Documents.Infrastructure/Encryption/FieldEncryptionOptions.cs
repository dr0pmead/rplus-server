namespace RPlus.Documents.Infrastructure.Encryption;

public sealed class FieldEncryptionOptions
{
    public const string SectionName = "Documents:Encryption";

    public string? MasterKey { get; set; }
}
