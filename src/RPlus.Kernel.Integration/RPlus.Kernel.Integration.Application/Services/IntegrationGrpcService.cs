// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IntegrationGrpcService
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Google.Protobuf;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlusGrpc.Integration;
using MediatR;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class IntegrationGrpcService : IntegrationService.IntegrationServiceBase
{
  private readonly ILogger<IntegrationGrpcService> _logger;
  private readonly IIntegrationRouteResolver _routeResolver;
  private readonly IGrpcReflectionCaller _dynamicCaller;
  private readonly IIntegrationDbContext _db;
  private readonly IIntegrationAuditService _auditService;
  private readonly IIntegrationRateLimiter _rateLimiter;

  public IntegrationGrpcService(
    ILogger<IntegrationGrpcService> logger,
    IIntegrationRouteResolver routeResolver,
    IGrpcReflectionCaller dynamicCaller,
    IIntegrationDbContext db,
    IIntegrationAuditService auditService,
    IIntegrationRateLimiter rateLimiter)
  {
    this._logger = logger;
    this._routeResolver = routeResolver;
    this._dynamicCaller = dynamicCaller;
    this._db = db;
    this._auditService = auditService;
    this._rateLimiter = rateLimiter;
  }

  public override async Task<ValidateKeyResponse> ValidateKey(
    ValidateKeyRequest request,
    ServerCallContext context)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(request.Secret))
        return new ValidateKeyResponse()
        {
          Success = false,
          Error = "invalid_secret"
        };

      string keyHash = IntegrationGrpcService.ComputeHash(request.Secret);
      IntegrationApiKey apiKey = await this._db.ApiKeys.Include<IntegrationApiKey, IntegrationPartner>((Expression<Func<IntegrationApiKey, IntegrationPartner>>) (k => k.Partner)).AsNoTracking<IntegrationApiKey>().FirstOrDefaultAsync<IntegrationApiKey>((Expression<Func<IntegrationApiKey, bool>>) (k => k.KeyHash == keyHash), context.CancellationToken);
      if (apiKey == null)
        return new ValidateKeyResponse()
        {
          Success = false,
          Error = "invalid_key_id"
        };
      if (apiKey.Status != "Active")
        return new ValidateKeyResponse()
        {
          Success = false,
          Error = "key_inactive"
        };
      if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        return new ValidateKeyResponse()
        {
          Success = false,
          Error = "key_expired"
        };
      if (!await this._rateLimiter.IsAllowedAsync(apiKey, (string) null, context.CancellationToken))
        return new ValidateKeyResponse()
        {
          Success = false,
          Error = "rate_limit_exceeded"
        };
      ValidateKeyResponse validateKeyResponse = new ValidateKeyResponse();
      validateKeyResponse.Success = true;
      Guid guid = apiKey.Id;
      validateKeyResponse.ApiKeyId = guid.ToString();
      Guid? partnerId = apiKey.PartnerId;
      ref Guid? local = ref partnerId;
      string str;
      if (!local.HasValue)
      {
        str = (string) null;
      }
      else
      {
        guid = local.GetValueOrDefault();
        str = guid.ToString();
      }
      if (str == null)
        str = string.Empty;
      validateKeyResponse.PartnerId = str;
      IntegrationPartner partner = apiKey.Partner;
      validateKeyResponse.IsDiscountPartner = partner != null && partner.IsDiscountPartner;
      validateKeyResponse.Status = apiKey.Status;
      return validateKeyResponse;
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error validating key");
      return new ValidateKeyResponse()
      {
        Success = false,
        Error = "internal_error"
      };
    }
  }

  private static string ComputeHash(string input)
  {
    if (string.IsNullOrEmpty(input))
      return string.Empty;
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToHexString(shA256.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
  }

  public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
  {
    string str = context.RequestHeaders.GetValue("x-trace-id") ?? Guid.NewGuid().ToString();
    this._logger.LogInformation("Ping received, TraceId: {TraceId}", (object) str);
    return Task.FromResult<PingResponse>(new PingResponse()
    {
      Message = "Integration Service OK",
      TraceId = str
    });
  }

  public override async Task<ProxyCallResponse> ProxyCall(
    ProxyCallRequest request,
    ServerCallContext context)
  {
    string traceId = string.IsNullOrWhiteSpace(request.TraceId) ? Guid.NewGuid().ToString() : request.TraceId;
    var stopwatch = Stopwatch.StartNew();
    var endpoint = request.Endpoint ?? string.Empty;
    var method = string.IsNullOrWhiteSpace(request.Method) ? "GET" : request.Method;
    var targetService = "unresolved";
    var statusCode = 500;
    string? errorMessage = null;

    try
    {
      var partnerId = ParseGuid(request.Context?.PartnerId);
      var route = await _routeResolver.ResolveAsync(endpoint, partnerId, context.CancellationToken);
      if (route == null)
      {
        statusCode = 404;
        errorMessage = "route_not_found";
        _logger.LogWarning("Route not found for endpoint: {Endpoint}, TraceId: {TraceId}", endpoint, traceId);
        return CreateErrorResponse(traceId, 404, "route_not_found");
      }

      targetService = route.TargetService;

      var apiKeyId = ParseGuid(request.Context?.ApiKeyId);
      if (apiKeyId.HasValue)
      {
        var apiKey = await _db.ApiKeys.FindAsync(new object[] { apiKeyId.Value }, context.CancellationToken);
        if (apiKey != null && !await _rateLimiter.IsAllowedAsync(apiKey, route.RoutePattern, context.CancellationToken))
        {
          statusCode = 429;
          errorMessage = "rate_limit_exceeded";
          return CreateErrorResponse(traceId, 429, "rate_limit_exceeded");
        }
      }

      var proxyResult = await _dynamicCaller.CallDynamicAsync(
        route.TargetHost,
        route.TargetService,
        route.TargetMethod,
        request.Body.ToByteArray(),
        BuildMetadata(request, traceId),
        context.CancellationToken);

      statusCode = proxyResult.StatusCode;
      errorMessage = proxyResult.Error;

      return new ProxyCallResponse
      {
        StatusCode = proxyResult.StatusCode,
        Body = proxyResult.Response != null ? ByteString.CopyFrom(proxyResult.Response) : ByteString.Empty,
        TraceId = traceId,
        Error = proxyResult.Error ?? string.Empty
      };
    }
    catch (Exception ex)
    {
      statusCode = 500;
      errorMessage = "internal_proxy_error";
      _logger.LogError(ex, "Unexpected error during ProxyCall, TraceId: {TraceId}", traceId);
      return CreateErrorResponse(traceId, 500, "internal_proxy_error");
    }
    finally
    {
      stopwatch.Stop();
      await _auditService.LogAsync(
        new IntegrationAuditLog
        {
          TraceId = traceId,
          PartnerId = ParseGuid(request.Context?.PartnerId),
          ApiKeyId = ParseGuid(request.Context?.ApiKeyId),
          RequestPath = endpoint,
          RequestMethod = method,
          TargetService = targetService ?? "unresolved",
          StatusCode = statusCode,
          DurationMs = stopwatch.ElapsedMilliseconds,
          ClientIp = context.Peer ?? string.Empty,
          ErrorMessage = errorMessage,
          Timestamp = DateTime.UtcNow,
          CreatedAt = DateTime.UtcNow
        },
        CancellationToken.None);
    }
  }

  private static ProxyCallResponse CreateErrorResponse(string traceId, int statusCode, string error)
  {
    return new ProxyCallResponse
    {
      StatusCode = statusCode,
      Body = ByteString.CopyFromUtf8($"{{\"error\":\"{error}\"}}"),
      TraceId = traceId,
      Error = error
    };
  }

  private static Metadata BuildMetadata(ProxyCallRequest request, string traceId)
  {
    var metadata = new Metadata
    {
      { "x-trace-id", traceId },
      { "x-forwarded-for-context", request.Context?.Context ?? "unknown" }
    };

    if (!string.IsNullOrEmpty(request.Context?.UserId))
    {
      metadata.Add("x-user-id", request.Context.UserId);
    }

    if (!string.IsNullOrEmpty(request.Context?.TenantId))
    {
      metadata.Add("x-tenant-id", request.Context.TenantId);
    }

    if (request.Context?.Permissions != null)
    {
      foreach (var permission in request.Context.Permissions)
      {
        metadata.Add("x-permission", permission);
      }
    }

    return metadata;
  }

  private static Guid? ParseGuid(string? value) =>
    Guid.TryParse(value, out var result) ? result : null;
}
