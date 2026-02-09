// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Api.Services.AccessGrpcService
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 809913E0-E790-491D-8B90-21CE464D2E43
// Assembly location: F:\RPlus Framework\Recovery\access\ExecuteService.dll

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Services;
using RPlus.Access.Domain.Entities;
using RPlus.SDK.Access.Events;
using RPlus.SDK.Eventing.Abstractions;
using RPlusGrpc.Access;
using RPlusGrpc.Audit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Api.Services;

public class AccessGrpcService : AccessService.AccessServiceBase
{
  private static readonly ConcurrentDictionary<string, long> s_lastAuditFailureTicksByType = new();

  private readonly IEffectiveRightsService _effectiveRightsService;
  private readonly IPermissionRegistry _permissionRegistry;
  private readonly IRootAccessService _rootAccessService;
  private readonly ILogger<AccessGrpcService> _logger;
  private readonly IIntegrationPermissionService _integrationPermissionService;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IEventPublisher _eventPublisher;
  private readonly PermissionManifestOptions _permissionManifestOptions;

  public AccessGrpcService(
    IEffectiveRightsService effectiveRightsService,
    IPermissionRegistry permissionRegistry,
    IRootAccessService rootAccessService,
    ILogger<AccessGrpcService> logger,
    IIntegrationPermissionService integrationPermissionService,
    IServiceScopeFactory scopeFactory,
    IEventPublisher eventPublisher,
    IOptions<PermissionManifestOptions> permissionManifestOptions)
  {
    this._effectiveRightsService = effectiveRightsService;
    this._permissionRegistry = permissionRegistry;
    this._rootAccessService = rootAccessService;
    this._logger = logger;
    this._integrationPermissionService = integrationPermissionService;
    this._scopeFactory = scopeFactory;
    this._eventPublisher = eventPublisher;
    this._permissionManifestOptions = permissionManifestOptions?.Value ?? new PermissionManifestOptions();
  }

  private string ResolveActor(ServerCallContext context)
  {
    return context.RequestHeaders.GetValue("x-user-id") ?? "System";
  }

  private async Task PublishAccessDecisionEventAsync(
    Guid userId,
    Guid tenantId,
    string permissionId,
    bool allowed,
    string reason,
    string scope,
    string contextJson)
  {
    Dictionary<string, string>? context = NormalizeContext(contextJson);
    if (!string.IsNullOrWhiteSpace(scope))
    {
      if (context == null)
        context = new Dictionary<string, string>();
      context["scope"] = scope;
    }
    AccessDecisionEvent accessDecisionEvent = new AccessDecisionEvent(Guid.NewGuid(), tenantId, userId, permissionId, null, allowed, reason, DateTime.UtcNow, context, false, (int?) null, (TimeSpan?) null, 0, null);
    await this._eventPublisher.PublishAsync<AccessDecisionEvent>(accessDecisionEvent, AccessEventTopics.AccessDecisionMade, userId.ToString());
  }

  private async Task PublishPermissionEventAsync<T>(
    T payload,
    string topic,
    string aggregateId)
    where T : class
  {
    try
    {
      await this._eventPublisher.PublishAsync<T>(payload, topic, aggregateId);
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Failed to publish Access event {Topic}", (object) topic);
    }
  }

  private static Dictionary<string, string>? NormalizeContext(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return null;
    try
    {
      Dictionary<string, JsonElement> dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new Dictionary<string, JsonElement>();
      return dictionary.ToDictionary<KeyValuePair<string, JsonElement>, string, string>((Func<KeyValuePair<string, JsonElement>, string>) (kv => kv.Key), (Func<KeyValuePair<string, JsonElement>, string>) (kv => kv.Value.ToString()));
    }
    catch
    {
      return null;
    }
  }

  private static Guid ParseGuidSafe(string? value)
  {
    if (Guid.TryParse(value, out Guid result))
      return result;
    return Guid.Empty;
  }

  private void LogAudit(
    string eventType,
    string severity,
    string actor,
    string action,
    string resource,
    Dictionary<string, string>? metadata = null)
  {
    Task.Run((Func<Task>) (async () =>
    {
      try
      {
        using (IServiceScope scope = this._scopeFactory.CreateScope())
        {
          AuditService.AuditServiceClient requiredService = scope.ServiceProvider.GetRequiredService<AuditService.AuditServiceClient>();
          RecordEventRequest request = new RecordEventRequest()
          {
            Source = "Access",
            EventType = eventType,
            Severity = severity,
            Actor = actor,
            Action = action,
            Resource = resource,
            OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow),
            TraceId = ""
          };
          if (metadata != null)
          {
            foreach (KeyValuePair<string, string> keyValuePair in metadata)
              request.Metadata.Add(keyValuePair.Key, keyValuePair.Value);
          }
          RecordEventResponse recordEventResponse = await requiredService.RecordEventAsync(request);
        }
      }
      catch (Exception ex)
      {
        // Fail-open: audit must never break business flows. Also throttle to avoid log spam if Audit is temporarily unavailable.
        long now = DateTime.UtcNow.Ticks;
        long last = 0;
        if (!s_lastAuditFailureTicksByType.TryGetValue(eventType, out last) || now - last > TimeSpan.FromSeconds(30.0).Ticks)
        {
          s_lastAuditFailureTicksByType[eventType] = now;
          this._logger.LogWarning(ex, "Failed to send audit event: {EventType}", (object) eventType);
        }
      }
    }));
  }

  public override async Task<CheckPermissionResponse> CheckPermission(
    CheckPermissionRequest request,
    ServerCallContext context)
  {
    Guid userId = Guid.Empty;
    Guid tenantId = Guid.Empty;
    AccessGrpcService accessGrpcService = this;

    async Task<CheckPermissionResponse> RespondAsync(
        bool allowed,
        string reason,
        string scope = "")
    {
        await this.PublishAccessDecisionEventAsync(userId, tenantId, request.PermissionId, allowed, reason, scope, request.Context ?? string.Empty);
        return new CheckPermissionResponse()
        {
          IsAllowed = allowed,
          Reason = reason,
          Scope = scope
        };
    }

    try
    {
      if (!Guid.TryParse(request.UserId, out userId) || !Guid.TryParse(request.TenantId, out tenantId))
        return await RespondAsync(false, "Invalid user or tenant ID");
      if (await accessGrpcService._rootAccessService.IsRootAsync(request.UserId, context.CancellationToken))
      {
        accessGrpcService.LogAudit(nameof (CheckPermission), "Info", request.UserId, "Check (Root)", request.PermissionId, new Dictionary<string, string>()
        {
          {
            "result",
            "allowed"
          },
          {
            "tenant_id",
            request.TenantId
          }
        });
        return await RespondAsync(true, "Allowed (RB)");
      }
      bool flag;
      if ((JsonSerializer.Deserialize<Dictionary<string, bool>>(await accessGrpcService._effectiveRightsService.GetEffectivePermissionsJsonAsync(userId, tenantId, request.Context, context.CancellationToken)) ?? new Dictionary<string, bool>()).TryGetValue(request.PermissionId, out flag))
      {
        accessGrpcService.LogAudit(nameof (CheckPermission), "Info", request.UserId, "Check", request.PermissionId, new Dictionary<string, string>()
        {
          {
            "result",
            flag.ToString()
          },
          {
            "tenant_id",
            request.TenantId
          }
        });
        return await RespondAsync(flag, flag ? string.Empty : "Permission denied by policy");
      }
      if (await accessGrpcService._permissionRegistry.RegisterAsync(request.PermissionId, request.ApplicationId, Array.Empty<string>(), context.CancellationToken))
      {
        await accessGrpcService._effectiveRightsService.InvalidateSnapshotAsync(userId, tenantId, context.CancellationToken);
        if ((JsonSerializer.Deserialize<Dictionary<string, bool>>(await accessGrpcService._effectiveRightsService.GetEffectivePermissionsJsonAsync(userId, tenantId, request.Context, context.CancellationToken)) ?? new Dictionary<string, bool>()).TryGetValue(request.PermissionId, out flag) & flag)
        {
          accessGrpcService.LogAudit(nameof (CheckPermission), "Info", request.UserId, "Check (Auto-Register)", request.PermissionId, new Dictionary<string, string>()
          {
            {
              "result",
              "allowed"
            },
            {
              "tenant_id",
              request.TenantId
            }
          });
          return await RespondAsync(true, "Access granted via auto-discovery");
        }
      }
      accessGrpcService.LogAudit(nameof (CheckPermission), "Info", request.UserId, "Check (Not Found)", request.PermissionId, new Dictionary<string, string>()
      {
        {
          "result",
          "denied"
        },
        {
          "tenant_id",
          request.TenantId
        }
      });
      return await RespondAsync(false, "Permission auto-registered but access denied");
    }
      catch (Exception ex)
      {
        accessGrpcService._logger.LogError(ex, "Error checking permission {PermissionId}", (object) request.PermissionId);
        return await RespondAsync(false, "Internal server error");
      }
  }

  public override async Task<GetEffectiveRightsResponse> GetEffectiveRights(
    GetEffectiveRightsRequest request,
    ServerCallContext context)
  {
    GetEffectiveRightsResponse effectiveRights;
    try
    {
      Guid result1;
      Guid result2;
      if (!Guid.TryParse(request.UserId, out result1) || !Guid.TryParse(request.TenantId, out result2))
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid IDs"));

      // Root users bypass policy evaluation; return a wildcard permission to keep the UX consistent
      // (frontend understands "*" as "all permissions").
      if (await this._rootAccessService.IsRootAsync(request.UserId, context.CancellationToken))
      {
        effectiveRights = new GetEffectiveRightsResponse()
        {
          PermissionsJson = JsonSerializer.Serialize(new Dictionary<string, bool>() { ["*"] = true }),
          Version = DateTime.UtcNow.Ticks
        };
        return effectiveRights;
      }

      string permissionsJsonAsync = await this._effectiveRightsService.GetEffectivePermissionsJsonAsync(result1, result2, request.Context, context.CancellationToken);
      effectiveRights = new GetEffectiveRightsResponse()
      {
        PermissionsJson = permissionsJsonAsync,
        Version = DateTime.UtcNow.Ticks
      };
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Error getting effective rights for {UserId}", (object) request.UserId);
      throw new RpcException(new Status(StatusCode.Internal, "Internal error"));
    }
    return effectiveRights;
  }

  public override async Task<PermissionsList> GetPermissions(
    RPlusGrpc.Access.Empty request,
    ServerCallContext context)
  {
    List<Permission> listAsync = await context.GetHttpContext().RequestServices.CreateScope().ServiceProvider.GetRequiredService<IAccessDbContext>().Permissions.OrderBy<Permission, Guid>((Expression<Func<Permission, Guid>>) (p => p.AppId)).ThenBy<Permission, string>((Expression<Func<Permission, string>>) (p => p.Resource)).ToListAsync<Permission>(context.CancellationToken);
    PermissionsList permissions = new PermissionsList();
    permissions.Permissions.AddRange(listAsync.Select<Permission, PermissionDto>((Func<Permission, PermissionDto>) (p => new PermissionDto()
    {
      PermissionId = p.Id,
      ApplicationId = p.AppId.ToString(),
      IsActive = p.Status == "ACTIVE"
    })));
    return permissions;
  }

  public override async Task<RPlusGrpc.Access.Empty> ActivatePermission(
    ActivatePermissionRequest request,
    ServerCallContext context)
  {
    IAccessDbContext db = context.GetHttpContext().RequestServices.CreateScope().ServiceProvider.GetRequiredService<IAccessDbContext>();
    Permission permission = await db.Permissions.FirstOrDefaultAsync<Permission>((Expression<Func<Permission, bool>>) (p => p.Id == request.PermissionId), context.CancellationToken);
    if (permission != null)
    {
      permission.Status = "ACTIVE";
      int num = await db.SaveChangesAsync(context.CancellationToken);
      string actor = this.ResolveActor(context);
      this.LogAudit(nameof (ActivatePermission), "Info", actor, "Activate", request.PermissionId);
      await this.PublishPermissionEventAsync(new PermissionActivatedEvent(request.PermissionId, ParseGuidSafe(request.ApplicationId), actor, DateTime.UtcNow), AccessEventTopics.PermissionActivated, request.PermissionId);
    }
    RPlusGrpc.Access.Empty empty = new RPlusGrpc.Access.Empty();
    db = (IAccessDbContext) null;
    return empty;
  }

  public override async Task<RPlusGrpc.Access.Empty> RegisterPermission(
    RegisterPermissionRequest request,
    ServerCallContext context)
  {
    AccessGrpcService accessGrpcService = this;
    RPlusGrpc.Access.Empty empty;
    try
    {
      int num = await accessGrpcService._permissionRegistry.RegisterAsync(request.PermissionId, request.ApplicationId, request.SupportedContexts.ToArray<string>(), context.CancellationToken) ? 1 : 0;
      string actor = accessGrpcService.ResolveActor(context);
      accessGrpcService.LogAudit(nameof (RegisterPermission), "Info", actor, "Register", request.PermissionId, new Dictionary<string, string>()
      {
        {
          "app_id",
          request.ApplicationId
        }
      });
      if (num != 0)
        await accessGrpcService.PublishPermissionEventAsync(new PermissionRegisteredEvent(request.PermissionId, ParseGuidSafe(request.ApplicationId), request.SupportedContexts?.ToList(), actor, DateTime.UtcNow), AccessEventTopics.PermissionRegistered, request.PermissionId);
      empty = new RPlusGrpc.Access.Empty();
    }
    catch (Exception ex)
    {
      accessGrpcService._logger.LogError(ex, "Failed to register permission {PermissionId}", (object) request.PermissionId);
      throw new RpcException(new Status(StatusCode.Internal, "Registration failed"));
    }
    return empty;
  }

  public override async Task<UpsertPermissionManifestResponse> UpsertPermissionManifest(
    UpsertPermissionManifestRequest request,
    ServerCallContext context)
  {
    AccessGrpcService accessGrpcService = this;
    try
    {
      string serviceName = (request.ServiceName ?? string.Empty).Trim();
      string appCode = (request.ApplicationId ?? string.Empty).Trim();

      if (string.IsNullOrWhiteSpace(serviceName) || serviceName.Length > 100)
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid service_name"));
      if (string.IsNullOrWhiteSpace(appCode) || appCode.Length > 50)
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid application_id"));

      if (!IsManifestCallerAuthorized(accessGrpcService, request, context))
        throw new RpcException(new Status(StatusCode.Unauthenticated, "Service not authorized"));

      using IServiceScope scope = context.GetHttpContext().RequestServices.CreateScope();
      IAccessDbContext db = scope.ServiceProvider.GetRequiredService<IAccessDbContext>();

      App app = await db.Apps.FirstOrDefaultAsync<App>(x => x.Code == appCode, context.CancellationToken);
      if (app == null)
      {
        app = new App()
        {
          Id = Guid.NewGuid(),
          Code = appCode,
          Name = appCode
        };
        db.Apps.Add(app);
        await db.SaveChangesAsync(context.CancellationToken);
      }

      DateTime now = DateTime.UtcNow;

      string[] permissionIds = request.Permissions
        .Select(p => (p.PermissionId ?? string.Empty).Trim())
        .Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 150)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

      // IMPORTANT: force LINQ Enumerable.Contains for EF translation.
      // On newer runtimes, `array.Contains(x)` may bind to MemoryExtensions.Contains(ReadOnlySpan<T>, T),
      // which EF cannot translate and can even crash during parameter evaluation.
      List<Permission> existingList = await db.Permissions
        .Where(p => System.Linq.Enumerable.Contains(permissionIds, p.Id))
        .ToListAsync(context.CancellationToken);

      Dictionary<string, Permission> existing = existingList.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

      int upserted = 0;
      foreach (PermissionManifestEntry entry in request.Permissions)
      {
        string permissionId = (entry.PermissionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(permissionId) || permissionId.Length > 150)
          continue;

        Permission permission;
        if (!existing.TryGetValue(permissionId, out permission))
        {
          permission = new Permission()
          {
            Id = permissionId,
            AppId = app.Id,
            CreatedAt = now,
            UpdatedAt = now,
            Status = "ACTIVE"
          };
          db.Permissions.Add(permission);
          existing[permissionId] = permission;
          upserted++;
        }

        (string resource, string action) = SplitResourceAction(permissionId);

        permission.AppId = app.Id;
        permission.Resource = resource;
        permission.Action = action;
        permission.Title = string.IsNullOrWhiteSpace(entry.Title) ? $"Discovered {permissionId}" : entry.Title.Trim();
        permission.Description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description.Trim();
        permission.SupportedContexts = entry.SupportedContexts
          .Where(x => !string.IsNullOrWhiteSpace(x))
          .Select(x => x.Trim())
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToArray();
        permission.SourceService = serviceName;
        if (permission.Status == "DEPRECATED")
          permission.Status = "ACTIVE";
        permission.UpdatedAt = now;
      }

      int deprecated = 0;
      if (request.MarkMissingAsDeprecated && permissionIds.Length > 0)
      {
        List<Permission> missing = await db.Permissions
          .Where(p =>
              p.AppId == app.Id &&
              p.SourceService == serviceName &&
              !System.Linq.Enumerable.Contains(permissionIds, p.Id) &&
              p.Status != "DEPRECATED")
          .ToListAsync(context.CancellationToken);

        foreach (Permission p in missing)
        {
          p.Status = "DEPRECATED";
          p.UpdatedAt = now;
        }
        deprecated = missing.Count;
      }

      await db.SaveChangesAsync(context.CancellationToken);

      string actor = accessGrpcService.ResolveActor(context);
      accessGrpcService.LogAudit(nameof (UpsertPermissionManifest), "Info", actor, "UpsertManifest", serviceName, new Dictionary<string, string>()
      {
        { "app_code", appCode },
        { "upserted", upserted.ToString() },
        { "deprecated", deprecated.ToString() }
      });

      return new UpsertPermissionManifestResponse()
      {
        Upserted = upserted,
        Deprecated = deprecated
      };
    }
    catch (RpcException)
    {
      throw;
    }
    catch (Exception ex)
    {
      accessGrpcService._logger.LogError(ex, "Failed to upsert permission manifest for {Service}", (object) request.ServiceName);
      throw new RpcException(new Status(StatusCode.Internal, "Manifest upsert failed"));
    }
  }

  private static (string Resource, string Action) SplitResourceAction(string permissionId)
  {
    string[] parts = permissionId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
      return ("unknown", "execute");
    if (parts.Length == 1)
      return (parts[0], "execute");

    string action = parts[parts.Length - 1];
    string resource = string.Join('.', parts.Take(parts.Length - 1));
    if (resource.Length > 100)
      resource = resource.Substring(0, 100);
    if (action.Length > 100)
      action = action.Substring(0, 100);
    return (resource, action);
  }

  private static bool IsManifestCallerAuthorized(
    AccessGrpcService service,
    UpsertPermissionManifestRequest request,
    ServerCallContext context)
  {
    PermissionManifestOptions options = service._permissionManifestOptions ?? new PermissionManifestOptions();
    IHostEnvironment env = context.GetHttpContext().RequestServices.GetRequiredService<IHostEnvironment>();

    string secret = options.SharedSecret ?? string.Empty;
    if (string.IsNullOrWhiteSpace(secret))
      return env.IsDevelopment() && options.AllowInDevelopmentWithoutSecret;

    string header = context.RequestHeaders.GetValue("x-rplus-service-secret") ?? string.Empty;
    if (!string.Equals(header, secret, StringComparison.Ordinal))
      return false;

    if (options.AllowedServices is { Length: > 0 })
    {
      string serviceName = (request.ServiceName ?? string.Empty).Trim();
      return options.AllowedServices.Any(s => string.Equals(s, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    return true;
  }

  public override async Task<GetIntegrationPermissionsResponse> GetIntegrationPermissions(
    GetIntegrationPermissionsRequest request,
    ServerCallContext context)
  {
    AccessGrpcService accessGrpcService = this;
    try
    {
      Guid result;
      if (!Guid.TryParse(request.ApiKeyId, out result))
        return new GetIntegrationPermissionsResponse()
        {
          Success = false,
          Error = "invalid_api_key_id",
          Decision = IntegrationDecision.DeniedPolicy
        };
      List<string> permissionsAsync = await accessGrpcService._integrationPermissionService.GetPermissionsAsync(result, (IDictionary<string, string>) request.ContextSignals, context.CancellationToken);
      GetIntegrationPermissionsResponse integrationPermissions = new GetIntegrationPermissionsResponse()
      {
        Success = true,
        Decision = IntegrationDecision.Allowed
      };
      integrationPermissions.Permissions.AddRange((IEnumerable<string>) permissionsAsync);
      accessGrpcService.LogAudit(nameof (GetIntegrationPermissions), "Info", request.ApiKeyId, "GetPermissions", "Permissions", new Dictionary<string, string>()
      {
        {
          "count",
          permissionsAsync.Count.ToString()
        }
      });
      return integrationPermissions;
    }
    catch (Exception ex)
    {
      accessGrpcService._logger.LogError(ex, "Error getting integration permissions for Key {KeyId}", (object) request.ApiKeyId);
      return new GetIntegrationPermissionsResponse()
      {
        Success = false,
        Error = "internal_error",
        Decision = IntegrationDecision.DeniedNotFound
      };
    }
  }

  public override async Task<RPlusGrpc.Access.Empty> GrantIntegrationPermission(
    GrantIntegrationPermissionRequest request,
    ServerCallContext context)
  {
    AccessGrpcService accessGrpcService = this;
    RPlusGrpc.Access.Empty empty;
    try
    {
      Guid apiKeyId;
      if (!Guid.TryParse(request.ApiKeyId, out apiKeyId))
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ApiKey ID"));
      IAccessDbContext db = context.GetHttpContext().RequestServices.CreateScope().ServiceProvider.GetRequiredService<IAccessDbContext>();
      if (await db.Permissions.FirstOrDefaultAsync<Permission>((Expression<Func<Permission, bool>>) (p => p.Id == request.PermissionId), context.CancellationToken) == null)
        db.Permissions.Add(new Permission()
        {
          Id = request.PermissionId,
          AppId = Guid.Empty,
          Status = "ACTIVE"
        });
      if (await db.IntegrationApiKeyPermissions.FirstOrDefaultAsync<IntegrationApiKeyPermission>((Expression<Func<IntegrationApiKeyPermission, bool>>) (p => p.ApiKeyId == apiKeyId && p.PermissionId == request.PermissionId), context.CancellationToken) == null)
      {
        db.IntegrationApiKeyPermissions.Add(new IntegrationApiKeyPermission()
        {
          ApiKeyId = apiKeyId,
          PermissionId = request.PermissionId,
          GrantedAt = DateTime.UtcNow
        });
        int num = await db.SaveChangesAsync(context.CancellationToken);
        string actor = accessGrpcService.ResolveActor(context);
        accessGrpcService.LogAudit(nameof (GrantIntegrationPermission), "Info", actor, "Grant", request.PermissionId, new Dictionary<string, string>()
        {
          {
            "api_key_id",
            request.ApiKeyId
          }
        });
        await accessGrpcService.PublishPermissionEventAsync(new IntegrationPermissionGrantedEvent(apiKeyId, request.PermissionId, actor, DateTime.UtcNow), AccessEventTopics.IntegrationPermissionGranted, apiKeyId.ToString());
      }
      empty = new RPlusGrpc.Access.Empty();
    }
    catch (Exception ex)
    {
      accessGrpcService._logger.LogError(ex, "Failed to grant permission {PermissionId} to key {KeyId}", (object) request.PermissionId, (object) request.ApiKeyId);
      throw new RpcException(new Status(StatusCode.Internal, "Grant failed"));
    }
    return empty;
  }

  public override async Task<RPlusGrpc.Access.Empty> RevokeIntegrationPermission(
    RevokeIntegrationPermissionRequest request,
    ServerCallContext context)
  {
    AccessGrpcService accessGrpcService = this;
    RPlusGrpc.Access.Empty empty;
    try
    {
      Guid apiKeyId;
      if (!Guid.TryParse(request.ApiKeyId, out apiKeyId))
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ApiKey ID"));
      IAccessDbContext db = context.GetHttpContext().RequestServices.CreateScope().ServiceProvider.GetRequiredService<IAccessDbContext>();
      IntegrationApiKeyPermission entity = await db.IntegrationApiKeyPermissions.FirstOrDefaultAsync<IntegrationApiKeyPermission>((Expression<Func<IntegrationApiKeyPermission, bool>>) (p => p.ApiKeyId == apiKeyId && p.PermissionId == request.PermissionId), context.CancellationToken);
      if (entity != null)
      {
        db.IntegrationApiKeyPermissions.Remove(entity);
        int num = await db.SaveChangesAsync(context.CancellationToken);
        string actor = accessGrpcService.ResolveActor(context);
        accessGrpcService.LogAudit(nameof (RevokeIntegrationPermission), "Info", actor, "Revoke", request.PermissionId, new Dictionary<string, string>()
        {
          {
            "api_key_id",
            request.ApiKeyId
          }
        });
        await accessGrpcService.PublishPermissionEventAsync(new IntegrationPermissionRevokedEvent(apiKeyId, request.PermissionId, actor, DateTime.UtcNow), AccessEventTopics.IntegrationPermissionRevoked, apiKeyId.ToString());
      }
      empty = new RPlusGrpc.Access.Empty();
    }
    catch (Exception ex)
    {
      accessGrpcService._logger.LogError(ex, "Failed to revoke permission {PermissionId} from key {KeyId}", (object) request.PermissionId, (object) request.ApiKeyId);
      throw new RpcException(new Status(StatusCode.Internal, "Revoke failed"));
    }
    return empty;
  }

}
