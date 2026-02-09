using System.Security.Cryptography;
using System.Text;

namespace RPlus.Documents.Infrastructure.Encryption;

public sealed class AesGcmFieldEncryptor : IFieldEncryptor
{
    private const string Prefix = "enc:";
    private readonly byte[]? _key;

    public AesGcmFieldEncryptor(FieldEncryptionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.MasterKey))
        {
            _key = Convert.FromBase64String(options.MasterKey);
        }
    }

    public bool IsConfigured => _key is { Length: 32 };

    public string Encrypt(string value)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(value))
            return value;

        using var aes = new AesGcm(_key!);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, plaintext, cipher, tag);

        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string value)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(value))
            return value;

        if (!value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return value;

        var raw = Convert.FromBase64String(value.Substring(Prefix.Length));
        var nonce = raw.AsSpan(0, 12);
        var tag = raw.AsSpan(12, 16);
        var cipher = raw.AsSpan(28);
        var plaintext = new byte[cipher.Length];

        using var aes = new AesGcm(_key!);
        aes.Decrypt(nonce, cipher, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
