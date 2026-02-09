// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IntegrationAuditService
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlusGrpc.Audit;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class IntegrationAuditService : IIntegrationAuditService
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<IntegrationAuditService> _logger;

  public IntegrationAuditService(
    IServiceScopeFactory scopeFactory,
    ILogger<IntegrationAuditService> logger)
  {
    this._scopeFactory = scopeFactory;
    this._logger = logger;
  }

  public async Task LogAsync(IntegrationAuditLog log, CancellationToken cancellationToken = default (CancellationToken))
  {
    Task.Run((Func<Task>) (async () =>
    {
      try
      {
        using (IServiceScope scope = this._scopeFactory.CreateScope())
        {
          AuditService.AuditServiceClient requiredService = scope.ServiceProvider.GetRequiredService<AuditService.AuditServiceClient>();
          RecordEventRequest recordEventRequest1 = new RecordEventRequest();
          recordEventRequest1.Source = "Integration";
          recordEventRequest1.EventType = ResolveEventType(log);
          recordEventRequest1.Severity = log.StatusCode >= 500 ? "Error" : (log.StatusCode >= 400 ? "Warning" : "Info");
          Guid? apiKeyId = log.ApiKeyId;
          ref Guid? local = ref apiKeyId;
          Guid valueOrDefault;
          string str1;
          if (!local.HasValue)
          {
            str1 = (string) null;
          }
          else
          {
            valueOrDefault = local.GetValueOrDefault();
            str1 = valueOrDefault.ToString();
          }
          if (str1 == null)
            str1 = "Anonymous";
          recordEventRequest1.Actor = str1;
          recordEventRequest1.Action = $"{log.RequestMethod} {log.RequestPath}";
          recordEventRequest1.Resource = log.TargetService;
          var occurredAt = log.Timestamp.Kind == DateTimeKind.Unspecified
              ? DateTime.SpecifyKind(log.Timestamp, DateTimeKind.Utc)
              : log.Timestamp.ToUniversalTime();
          recordEventRequest1.OccurredAt = Timestamp.FromDateTime(occurredAt);
          recordEventRequest1.TraceId = log.TraceId;
          RecordEventRequest recordEventRequest2 = recordEventRequest1;
          recordEventRequest2.Metadata.Add("status_code", log.StatusCode.ToString());
          recordEventRequest2.Metadata.Add("duration_ms", log.DurationMs.ToString());
          recordEventRequest2.Metadata.Add("client_ip", log.ClientIp);
          recordEventRequest2.Metadata.Add("request_path", log.RequestPath);
          recordEventRequest2.Metadata.Add("request_method", log.RequestMethod);
          if (!string.IsNullOrWhiteSpace(log.Details))
          {
            try
            {
              using var doc = JsonDocument.Parse(log.Details);
              if (doc.RootElement.ValueKind == JsonValueKind.Object)
              {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                  var value = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.ToString();
                  if (!string.IsNullOrWhiteSpace(value))
                  {
                    recordEventRequest2.Metadata[prop.Name] = value;
                  }
                }
              }
            }
            catch
            {
              // ignore malformed details
            }
          }
          Guid? partnerId = log.PartnerId;
          if (partnerId.HasValue)
          {
            MapField<string, string> metadata = recordEventRequest2.Metadata;
            partnerId = log.PartnerId;
            valueOrDefault = partnerId.Value;
            string str2 = valueOrDefault.ToString();
            metadata.Add("partner_id", str2);
          }
          if (log.ApiKeyId.HasValue)
          {
            recordEventRequest2.Metadata.Add("api_key_id", log.ApiKeyId.Value.ToString());
          }
          if (!string.IsNullOrEmpty(log.ErrorMessage))
            recordEventRequest2.Metadata.Add("error_message", log.ErrorMessage);
          RecordEventRequest request = recordEventRequest2;
          DateTime? deadline = new DateTime?();
          CancellationToken cancellationToken1 = new CancellationToken();
          RecordEventResponse recordEventResponse = await requiredService.RecordEventAsync(request, deadline: deadline, cancellationToken: cancellationToken1);
        }
      }
      catch (Exception ex)
      {
        this._logger.LogError(ex, "Failed to send audit event to Audit Service. TraceId: {TraceId}", (object) log.TraceId);
      }
    }), CancellationToken.None);
    await Task.CompletedTask;
  }

  private static string ResolveEventType(IntegrationAuditLog log)
  {
    if (string.Equals(log.Action, "scan", StringComparison.OrdinalIgnoreCase) ||
        log.RequestPath.Contains("/scan", StringComparison.OrdinalIgnoreCase))
    {
      return "IntegrationScan";
    }

    return "ProxyRequest";
  }
}
