// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.JwtKeyRotationService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Application.Models;
using RPlus.Auth.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class JwtKeyRotationService : BackgroundService
{
  private readonly IJwtKeyStore _store;
  private readonly IOptionsMonitor<JwtOptions> _options;
  private readonly ILogger<JwtKeyRotationService> _logger;

  public JwtKeyRotationService(
    IJwtKeyStore store,
    IOptionsMonitor<JwtOptions> options,
    ILogger<JwtKeyRotationService> logger)
  {
    this._store = store;
    this._options = options;
    this._logger = logger;
  }

  public override Task StartAsync(CancellationToken cancellationToken)
  {
    this.EnsureInitializedAsync().GetAwaiter().GetResult();
    return base.StartAsync(cancellationToken);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    await this.EnsureInitializedAsync();
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        this.RotateIfNeeded();
        this._store.CleanupExpiredKeys();
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "JWT key rotation failed.");
      }
      await Task.Delay(TimeSpan.FromMinutes(5L), stoppingToken);
    }
  }

  private Task EnsureInitializedAsync()
  {
    if (this._store.GetActiveKey() != (JwtKeyMaterial) null)
      return Task.CompletedTask;
    JwtOptions currentValue = this._options.CurrentValue;
    DateTimeOffset utcNow = DateTimeOffset.UtcNow;
    DateTimeOffset expiresAt = utcNow.AddHours((double) Math.Max(1, currentValue.RetainForHours));
    string privatePem = currentValue.PrivateKeyPem ?? currentValue.SigningKey;
    if (!string.IsNullOrWhiteSpace(privatePem))
    {
      JwtKeyMaterial fromPrivatePem = JwtKeyMaterialFactory.CreateFromPrivatePem(privatePem, utcNow, expiresAt);
      this._store.SaveActiveKey(fromPrivatePem);
      this._logger.LogInformation("JWT key initialized from configured private key. kid={KeyId}", (object) fromPrivatePem.KeyId);
      return Task.CompletedTask;
    }
    JwtKeyMaterial material = JwtKeyMaterialFactory.GenerateNew(Math.Max(2048 /*0x0800*/, currentValue.KeySize), utcNow, expiresAt);
    this._store.SaveActiveKey(material);
    this._logger.LogInformation("JWT key generated on startup. kid={KeyId}", (object) material.KeyId);
    return Task.CompletedTask;
  }

  private void RotateIfNeeded()
  {
    JwtKeyMaterial activeKey = this._store.GetActiveKey();
    if (activeKey == (JwtKeyMaterial) null)
    {
      this.EnsureInitializedAsync();
    }
    else
    {
      JwtOptions currentValue = this._options.CurrentValue;
      int num = Math.Max(1, currentValue.RotateEveryHours);
      int hours = Math.Max(num, currentValue.RetainForHours);
      DateTimeOffset utcNow = DateTimeOffset.UtcNow;
      if (utcNow - activeKey.CreatedAt < TimeSpan.FromHours(num))
        return;
      JwtKeyMaterial material = JwtKeyMaterialFactory.GenerateNew(Math.Max(2048 /*0x0800*/, currentValue.KeySize), utcNow, utcNow.AddHours((double) hours));
      this._store.SaveActiveKey(material);
      this._logger.LogInformation("JWT key rotated. new_kid={KeyId}", (object) material.KeyId);
    }
  }
}
