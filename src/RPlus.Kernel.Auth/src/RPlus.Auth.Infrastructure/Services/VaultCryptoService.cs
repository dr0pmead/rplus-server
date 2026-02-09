using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public sealed class VaultCryptoService : IVaultCryptoService
{
    private readonly byte[] _key;
    private readonly ILogger<VaultCryptoService> _logger;

    public VaultCryptoService(IConfiguration configuration, ILogger<VaultCryptoService> logger)
    {
        _logger = logger;

        var raw =
            configuration["VAULT_MASTER_KEY"]
            ?? configuration["Vault:MasterKey"]
            ?? configuration["Vault__MasterKey"]
            ?? string.Empty;

        _key = ParseKey(raw);
    }

    public Task<string> EncryptToBase64Async(string plaintext, CancellationToken ct)
    {
        if (plaintext is null)
            return Task.FromResult(string.Empty);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        Span<byte> nonce = stackalloc byte[12];
        RandomNumberGenerator.Fill(nonce);

        var cipher = new byte[plainBytes.Length];
        Span<byte> tag = stackalloc byte[16];

        using (var aes = new AesGcm(_key, 16))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
        nonce.CopyTo(combined.AsSpan(0, nonce.Length));
        tag.CopyTo(combined.AsSpan(nonce.Length, tag.Length));
        cipher.CopyTo(combined.AsSpan(nonce.Length + tag.Length));

        return Task.FromResult(Convert.ToBase64String(combined));
    }

    public Task<string> DecryptFromBase64Async(string ciphertextBase64, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ciphertextBase64))
            return Task.FromResult(string.Empty);

        var combined = Convert.FromBase64String(ciphertextBase64);
        if (combined.Length < 12 + 16)
            throw new CryptographicException("Invalid ciphertext.");

        var nonce = combined.AsSpan(0, 12);
        var tag = combined.AsSpan(12, 16);
        var cipher = combined.AsSpan(28);

        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, 16))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Task.FromResult(Encoding.UTF8.GetString(plain));
    }

    private byte[] ParseKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Fallback for local/dev setups: derive a stable 32-byte key from the cluster internal secret.
            // Production should provide an explicit `VAULT_MASTER_KEY` (base64, 32 bytes) and rotate via deployment process.
            var fallback = Environment.GetEnvironmentVariable("RPLUS_INTERNAL_SERVICE_SECRET");
            if (string.IsNullOrWhiteSpace(fallback))
                throw new InvalidOperationException("VAULT_MASTER_KEY is not configured.");

            return SHA256.HashData(Encoding.UTF8.GetBytes(fallback));
        }

        try
        {
            var key = Convert.FromBase64String(raw);
            if (key.Length != 32)
                throw new InvalidOperationException("VAULT_MASTER_KEY must decode to 32 bytes.");
            return key;
        }
        catch (FormatException)
        {
            _logger.LogError("VAULT_MASTER_KEY is not valid base64.");
            throw;
        }
    }
}
