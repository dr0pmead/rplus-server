// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Infrastructure.Messaging.KafkaAuditPublisher
// Assembly: RPlus.Kernel.Audit.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 271DD6D6-68D7-47FD-8F9A-65D4B328CF02
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Infrastructure.dll

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Audit.Application.Commands.RecordAuditEvent;
using RPlus.Kernel.Audit.Domain.Entities;
using RPlus.SDK.Audit.Events;

#nullable enable
namespace RPlus.Kernel.Audit.Infrastructure.Messaging;

public class KafkaAuditPublisher : IAuditPublisher
{
  private readonly IProducer<string, string> _producer;
  private readonly ILogger<KafkaAuditPublisher> _logger;
  private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
  private const string TopicName = "kernel.audit.events";

  public KafkaAuditPublisher(ProducerConfig config, ILogger<KafkaAuditPublisher> logger)
  {
    this._producer = new ProducerBuilder<string, string>((IEnumerable<KeyValuePair<string, string>>) config).Build();
    this._logger = logger;
  }

  public async Task PublishAsync(AuditEvent auditEvent)
  {
    try
    {
      var payload = new AuditEventPayload
      {
        EventId = auditEvent.Id,
        Source = AuditEnumMapper.ToSdkSource(auditEvent.Source),
        EventType = AuditEnumMapper.ToSdkEventType(auditEvent.EventType),
        Severity = AuditEnumMapper.ToSdkSeverity(auditEvent.Severity),
        Actor = auditEvent.Actor,
        Action = auditEvent.Action,
        Resource = auditEvent.Resource,
        Metadata = auditEvent.Metadata,
        Timestamp = auditEvent.Timestamp,
        PreviousEventHash = auditEvent.PreviousEventHash,
        Signature = auditEvent.Signature,
        SignerId = auditEvent.SignerId
      };

      DeliveryResult<string, string> deliveryResult = await this._producer.ProduceAsync("kernel.audit.events", new Message<string, string>()
      {
        Key = auditEvent.Id.ToString(),
        Value = JsonSerializer.Serialize(payload, _serializerOptions)
      });
      this._logger.LogInformation("[Audit] Published event {EventId} to Kafka: {Topic}", (object) auditEvent.Id, (object) "kernel.audit.events");
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "[Audit] Failed to publish event {EventId} to Kafka", (object) auditEvent.Id);
    }
  }
}
