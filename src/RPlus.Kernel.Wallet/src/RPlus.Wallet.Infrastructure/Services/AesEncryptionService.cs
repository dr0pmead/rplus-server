using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Infrastructure.Services;

public sealed class AesEncryptionService : IEncryptionService
{
    private readonly Dictionary<string, byte[]> _keys = new();
    private readonly string _currentKeyId;

    public AesEncryptionService(IConfiguration configuration)
    {
        _currentKeyId = configuration["Encryption:CurrentKeyId"] ?? configuration["Wallet:KeyId"] ?? "v1";
        var section = configuration.GetSection("Encryption:Keys");
        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                if (!string.IsNullOrEmpty(child.Value))
                {
                    _keys[child.Key] = Convert.FromHexString(child.Value);
                }
            }
        }

        if (_keys.ContainsKey(_currentKeyId))
        {
            return;
        }

        var fallback = configuration["Encryption:Key"] ?? configuration["Wallet:EncryptionKey"];
        _keys[_currentKeyId] = !string.IsNullOrEmpty(fallback)
            ? Convert.FromHexString(fallback)
            : new byte[32];
    }

    public string GetCurrentKeyId() => _currentKeyId;

    public byte[] Encrypt(long value) => EncryptBytes(BitConverter.GetBytes(value), _currentKeyId);

    public byte[] Encrypt(string value) => EncryptBytes(Encoding.UTF8.GetBytes(value), _currentKeyId);

    public long DecryptLong(byte[] encrypted, string keyId) =>
        BitConverter.ToInt64(DecryptBytes(encrypted, keyId));

    public string DecryptString(byte[] encrypted, string keyId) =>
        Encoding.UTF8.GetString(DecryptBytes(encrypted, keyId));

    private byte[] EncryptBytes(byte[] plaintext, string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new InvalidOperationException($"Encryption Key {keyId} not found");
        }

        using var aes = new AesGcm(key, 16);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var combined = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length + ciphertext.Length, tag.Length);
        return combined;
    }

    private byte[] DecryptBytes(byte[] encryptedAndTag, string keyId)
    {
        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new InvalidOperationException($"Decryption Key {keyId} not found");
        }

        const int nonceSize = 12;
        const int tagSize = 16;
        var cipherLength = encryptedAndTag.Length - nonceSize - tagSize;
        if (cipherLength < 0)
        {
            throw new CryptographicException("Invalid encrypted data");
        }

        var nonce = encryptedAndTag.AsSpan(0, nonceSize);
        var ciphertext = encryptedAndTag.AsSpan(nonceSize, cipherLength);
        var tag = encryptedAndTag.AsSpan(nonceSize + cipherLength, tagSize);
        var plaintext = new byte[cipherLength];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
