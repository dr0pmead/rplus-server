// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.SystemTransactionSignatureService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class SystemTransactionSignatureService : ISystemTransactionSignatureService
{
  private readonly IConfiguration _configuration;
  private readonly ILogger<SystemTransactionSignatureService> _logger;

  public SystemTransactionSignatureService(
    IConfiguration configuration,
    ILogger<SystemTransactionSignatureService> logger)
  {
    this._configuration = configuration;
    this._logger = logger;
  }

  public string CreateSignature(
    Guid userId,
    TransactionType type,
    int points,
    string operationId,
    DateTime? expiresAt,
    DateTime timestamp)
  {
    string s = this._configuration.GetValue<string>("SystemTransaction:SigningSecret");
    if (string.IsNullOrWhiteSpace(s))
    {
      this._logger.LogError("SystemTransaction:SigningSecret is not configured. Cannot create transaction signature.");
      throw new InvalidOperationException("SystemTransaction:SigningSecret is not configured.");
    }
    try
    {
      byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
      {
        userId = userId.ToString(),
        type = type.ToString(),
        points = points,
        operationId = operationId,
        expiresAt = expiresAt?.ToString("O"),
        timestamp = timestamp.ToString("O")
      }, new JsonSerializerOptions()
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
      }));
      using (HMACSHA256 hmacshA256 = new HMACSHA256(Convert.FromBase64String(s)))
        return Convert.ToHexString(hmacshA256.ComputeHash(bytes)).ToLowerInvariant();
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error creating system transaction signature. UserId={UserId}, OperationId={OperationId}", (object) userId, (object) operationId);
      throw;
    }
  }
}
