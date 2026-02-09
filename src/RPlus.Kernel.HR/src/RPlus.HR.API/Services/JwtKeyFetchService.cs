using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RPlusGrpc.Auth;
using System.Security.Cryptography;

namespace RPlus.HR.Api.Services;

public sealed class JwtKeyFetchService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<JwtKeyFetchService> _logger;
    private readonly IConfiguration _configuration;
    private readonly JwtKeyCache _cache;
    private readonly string _authGrpcAddress;

    public JwtKeyFetchService(ILogger<JwtKeyFetchService> logger, IConfiguration configuration, JwtKeyCache cache)
    {
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        _authGrpcAddress = $"http://{configuration["AUTH_GRPC_HOST"] ?? "rplus-kernel-auth"}:{configuration["AUTH_GRPC_PORT"] ?? "5007"}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FetchWithRetryAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await FetchOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task FetchWithRetryAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested)
        {
            var success = await FetchOnceAsync(ct).ConfigureAwait(false);
            if (success)
                return;

            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            delay = TimeSpan.FromSeconds(Math.Min(MaxRetryDelay.TotalSeconds, delay.TotalSeconds * 2));
        }
    }

    private async Task<bool> FetchOnceAsync(CancellationToken ct)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_authGrpcAddress);
            var client = new AuthKeyService.AuthKeyServiceClient(channel);
            var response = await client.GetPublicKeysAsync(new GetPublicKeysRequest(), cancellationToken: ct).ConfigureAwait(false);

            var keys = new List<SecurityKey>();
            foreach (var key in response.Keys)
            {
                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(key.Pem.AsSpan());
                    var rsaKey = new RsaSecurityKey(rsa) { KeyId = key.Kid };
                    keys.Add(rsaKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import public key {Kid}", key.Kid);
                }
            }

            if (keys.Count == 0)
            {
                _logger.LogWarning("Auth returned 0 public keys; keeping existing key cache");
                return false;
            }

            _cache.UpdateKeys(keys);
            _logger.LogInformation("Fetched {Count} public keys from Auth service", keys.Count);
            return true;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Auth public key fetch unavailable (will retry)");
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch public keys from Auth service at {Address}", _authGrpcAddress);
            return false;
        }
    }
}
