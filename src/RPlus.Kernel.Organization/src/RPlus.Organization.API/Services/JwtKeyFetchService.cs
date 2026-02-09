// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Api.Services.JwtKeyFetchService
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 8ABF1D32-8F85-446A-8A49-54981F839476
// Assembly location: F:\RPlus Framework\Recovery\organization\ExecuteService.dll

using Google.Protobuf.Collections;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RPlusGrpc.Auth;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Organization.Api.Services;

public class JwtKeyFetchService : BackgroundService
{
  private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5.0);
  private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30.0);

  private readonly ILogger<JwtKeyFetchService> _logger;
  private readonly IConfiguration _configuration;
  private readonly string _authGrpcAddress;
  private readonly JwtKeyCache _keyCache;

  public JwtKeyFetchService(
    ILogger<JwtKeyFetchService> logger,
    IConfiguration configuration,
    JwtKeyCache keyCache)
  {
    this._logger = logger;
    this._configuration = configuration;
    this._keyCache = keyCache;
    this._authGrpcAddress = $"http://{configuration["AUTH_GRPC_HOST"] ?? "rplus-kernel-auth"}:{configuration["AUTH_GRPC_PORT"] ?? "5007"}";
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await this.FetchWithRetryAsync(stoppingToken);
    using (PeriodicTimer timer = new PeriodicTimer(RefreshInterval))
    {
      while (true)
      {
        if (await timer.WaitForNextTickAsync(stoppingToken))
          await this.FetchOnceAsync(stoppingToken);
        else
          break;
      }
    }
  }

  private async Task FetchWithRetryAsync(CancellationToken ct)
  {
    TimeSpan delay = TimeSpan.FromSeconds(1.0);
    while (!ct.IsCancellationRequested)
    {
      if (await this.FetchOnceAsync(ct))
        return;
      try
      {
        await Task.Delay(delay, ct);
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested)
      {
        return;
      }
      delay = TimeSpan.FromSeconds(Math.Min(MaxRetryDelay.TotalSeconds, delay.TotalSeconds * 2.0));
    }
  }

  private async Task<bool> FetchOnceAsync(CancellationToken cancellationToken)
  {
    try
    {
      using (GrpcChannel channel = GrpcChannel.ForAddress(this._authGrpcAddress))
      {
        AuthKeyService.AuthKeyServiceClient keyServiceClient = new AuthKeyService.AuthKeyServiceClient((ChannelBase) channel);
        GetPublicKeysRequest request = new GetPublicKeysRequest();
        CancellationToken cancellationToken1 = cancellationToken;
        DateTime? deadline = new DateTime?();
        CancellationToken cancellationToken2 = cancellationToken1;
        GetPublicKeysResponse publicKeysAsync = await keyServiceClient.GetPublicKeysAsync(request, deadline: deadline, cancellationToken: cancellationToken2);
        List<SecurityKey> keys = new List<SecurityKey>();
        foreach (PublicKeyInfo key in (RepeatedField<PublicKeyInfo>) publicKeysAsync.Keys)
        {
          try
          {
            RSA rsa = RSA.Create();
            rsa.ImportFromPem(key.Pem.AsSpan());
            RsaSecurityKey rsaSecurityKey1 = new RsaSecurityKey(rsa);
            rsaSecurityKey1.KeyId = key.Kid;
            RsaSecurityKey rsaSecurityKey2 = rsaSecurityKey1;
            keys.Add((SecurityKey) rsaSecurityKey2);
          }
          catch (Exception ex)
          {
            this._logger.LogWarning(ex, "Failed to import public key {Kid}", (object) key.Kid);
          }
        }
        if (keys.Count == 0)
        {
          this._logger.LogWarning("Auth returned 0 public keys; keeping existing key cache");
          return false;
        }

        this._keyCache.UpdateKeys((IReadOnlyList<SecurityKey>) keys);
        this._logger.LogInformation("Fetched {Count} public keys from Auth service", (object) keys.Count);
      }

      return true;
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
    {
      this._logger.LogWarning(ex, "Auth public key fetch unavailable (will retry)");
      return false;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      return false;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to fetch public keys from Auth service at {Address}", (object) this._authGrpcAddress);
      return false;
    }
  }
}
