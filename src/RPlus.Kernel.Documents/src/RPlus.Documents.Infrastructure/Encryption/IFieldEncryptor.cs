namespace RPlus.Documents.Infrastructure.Encryption;

public interface IFieldEncryptor
{
    string Encrypt(string value);
    string Decrypt(string value);
    bool IsConfigured { get; }
}
