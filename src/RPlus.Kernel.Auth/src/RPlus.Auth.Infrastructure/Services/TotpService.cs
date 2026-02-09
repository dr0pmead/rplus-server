using RPlus.Auth.Application.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public sealed class TotpService : ITotpService
{
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    public string GenerateSecretBase32(int byteLength = 20)
    {
        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base32Encode(bytes);
    }

    public string BuildOtpAuthUri(string issuer, string accountName, string secretBase32)
    {
        var safeIssuer = Uri.EscapeDataString(issuer);
        var safeAccount = Uri.EscapeDataString(accountName);
        var safeSecret = Uri.EscapeDataString(secretBase32);
        return $"otpauth://totp/{safeIssuer}:{safeAccount}?secret={safeSecret}&issuer={safeIssuer}&digits={Digits}&period={PeriodSeconds}";
    }

    public Task<bool> VerifyAsync(string secretBase32, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
            return Task.FromResult(false);

        var normalizedCode = code.Trim().Replace(" ", string.Empty);
        if (normalizedCode.Length != Digits || !ulong.TryParse(normalizedCode, out _))
            return Task.FromResult(false);

        var secret = Base32Decode(secretBase32);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = now / PeriodSeconds;

        // Allow ±1 timestep drift
        for (var offset = -1; offset <= 1; offset++)
        {
            var expected = ComputeTotp(secret, timestep + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(normalizedCode)))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private static string ComputeTotp(byte[] secret, long timestep)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(timestep & 0xFF);
            timestep >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            (hash[offset + 3]);

        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        if (data.Length == 0) return string.Empty;

        var output = new StringBuilder((data.Length + 4) / 5 * 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 31;
                bitsLeft -= 5;
                output.Append(alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 31;
            output.Append(alphabet[index]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

        input = input.Trim().Replace("=", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

        var output = new byte[input.Length * 5 / 8];
        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;

        foreach (var c in input)
        {
            var val = c switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= '2' and <= '7' => c - '2' + 26,
                _ => -1
            };

            if (val < 0) continue;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                output[index++] = (byte)((buffer >> (bitsLeft - 8)) & 255);
                bitsLeft -= 8;
            }
        }

        if (index == output.Length) return output;
        var trimmed = new byte[index];
        Array.Copy(output, trimmed, index);
        return trimmed;
    }
}

