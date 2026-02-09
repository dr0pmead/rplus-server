// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Eventing.EventEnvelope`1
// Assembly: RPlus.SDK.Eventing, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 33A42332-9F9D-4559-BE1F-4385252A1184
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Eventing.dll
// XML documentation location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Eventing.xml

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Eventing;

/// <summary>
/// The standard event envelope for all RPlus system events.
/// This structure is mandatory for all domain events to ensure consistency,
/// traceability, and idempotency across the ecosystem.
/// </summary>
/// <typeparam name="T">The type of the event payload.</typeparam>
public class EventEnvelope<T>
{
  /// <summary>
  /// Unique identifier for this specific event instance.
  /// Used for deduplication and idempotency checks.
  /// </summary>
  public Guid EventId { get; set; } = Guid.NewGuid();

  /// <summary>
  /// The distributed trace identifier to correlate events across services.
  /// Should be propagated from the initiating HTTP request or operation.
  /// </summary>
  public Guid TraceId { get; set; }

  /// <summary>
  /// The type of the event in the format 'domain.entity.action.version'.
  /// Example: 'users.user.created.v1'
  /// </summary>
  public string EventType { get; set; } = string.Empty;

  /// <summary>
  /// The UTC timestamp when the event occurred (not when it was published).
  /// </summary>
  public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// The name of the service that produced the event.
  /// Example: 'rplus.users'
  /// </summary>
  public string Source { get; set; } = string.Empty;

  /// <summary>
  /// The unique identifier of the aggregate root (entity) that this event pertains to.
  /// Example: 'userId', 'walletId'.
  /// </summary>
  public string AggregateId { get; set; } = string.Empty;

  /// <summary>The actual domain event data.</summary>
  public T? Payload { get; set; }

  /// <summary>
  /// Optional metadata for context (e.g., user agent, IP address, causation ID).
  /// </summary>
  public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

  /// <summary>Default constructor for serialization.</summary>
  public EventEnvelope()
  {
  }

  /// <summary>Creates a new event envelope.</summary>
  public EventEnvelope(
    T payload,
    string source,
    string eventType,
    string aggregateId,
    Guid traceId)
  {
    this.Payload = payload;
    this.Source = source;
    this.EventType = eventType;
    this.AggregateId = aggregateId;
    this.TraceId = traceId;
    this.OccurredAt = DateTime.UtcNow;
    this.EventId = Guid.NewGuid();
  }
}
