using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Application.Interfaces;

public interface ITotpService
{
    string GenerateSecretBase32(int byteLength = 20);
    string BuildOtpAuthUri(string issuer, string accountName, string secretBase32);
    Task<bool> VerifyAsync(string secretBase32, string code, CancellationToken ct);
}

