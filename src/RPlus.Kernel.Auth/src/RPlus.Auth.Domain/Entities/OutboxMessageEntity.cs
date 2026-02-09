// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Domain.Entities.OutboxMessageEntity
// Assembly: RPlus.Auth.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 30F033C3-72B9-4343-BF9A-347F69FE04BB
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Domain.dll

using System;

#nullable enable
namespace RPlus.Auth.Domain.Entities;

public sealed class OutboxMessageEntity
{
  public Guid Id { get; set; }

  public string Topic { get; set; } = string.Empty;

  public string EventType { get; set; } = string.Empty;

  public string Payload { get; set; } = string.Empty;

  public string AggregateId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }

  public DateTime? ProcessedAt { get; set; }

  public DateTime? SentAt { get; set; }

  public string Status { get; set; } = "Pending";

  public int RetryCount { get; set; }

  public int MaxRetries { get; set; } = 3;

  public DateTime? NextRetryAt { get; set; }

  public string? ErrorMessage { get; set; }

  public string? ErrorStackTrace { get; set; }

  public DateTime? LockedUntil { get; set; }

  public string? LockedBy { get; set; }

  public static class Statuses
  {
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
    public const string DeadLetter = "DeadLetter";
  }
}
