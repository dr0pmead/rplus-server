using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Application.Interfaces;

public interface IVaultCryptoService
{
    Task<string> EncryptToBase64Async(string plaintext, CancellationToken ct);
    Task<string> DecryptFromBase64Async(string ciphertextBase64, CancellationToken ct);
}

