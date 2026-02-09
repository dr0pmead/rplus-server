namespace RPlus.Wallet.Domain.Services;

#nullable enable
public interface IEncryptionService
{
    byte[] Encrypt(long value);
    byte[] Encrypt(string value);
    long DecryptLong(byte[] encrypted, string keyId);
    string DecryptString(byte[] encrypted, string keyId);
    string GetCurrentKeyId();
}
