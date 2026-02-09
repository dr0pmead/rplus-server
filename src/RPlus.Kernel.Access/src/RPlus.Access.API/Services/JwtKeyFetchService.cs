using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RPlusGrpc.Auth;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Access.Api.Services;

public sealed class JwtKeyFetchService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly ILogger<JwtKeyFetchService> _logger;
    private readonly AuthKeyService.AuthKeyServiceClient _client;
    private readonly JwtKeyCache _keyCache;

    public JwtKeyFetchService(
        ILogger<JwtKeyFetchService> logger,
        AuthKeyService.AuthKeyServiceClient client,
        JwtKeyCache keyCache)
    {
        _logger = logger;
        _client = client;
        _keyCache = keyCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FetchWithRetryAsync(stoppingToken);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FetchOnceAsync(stoppingToken);
        }
    }

    private async Task FetchWithRetryAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested)
        {
            var success = await FetchOnceAsync(ct);
            if (success)
                return;

            try
            {
                await Task.Delay(delay, ct);
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
            var response = await _client.GetPublicKeysAsync(new GetPublicKeysRequest(), cancellationToken: ct);

            var keys = new List<SecurityKey>(capacity: response.Keys.Count);
            foreach (var key in response.Keys)
            {
                try
                {
                    var rsa = RSA.Create();
                    rsa.ImportFromPem(key.Pem.AsSpan());
                    keys.Add(new RsaSecurityKey(rsa) { KeyId = key.Kid });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to import public key {Kid}", key.Kid);
                }
            }

            _keyCache.UpdateKeys(keys);
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
            _logger.LogError(ex, "Failed to fetch public keys from Auth service");
            return false;
        }
    }
}

