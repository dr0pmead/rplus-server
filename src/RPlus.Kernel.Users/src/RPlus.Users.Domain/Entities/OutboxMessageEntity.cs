// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Domain.Entities.OutboxMessageEntity
// Assembly: RPlus.Users.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EC28E05B-8FBC-4E16-9BDA-B90404FEB127
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Domain.dll

using System;

#nullable enable
namespace RPlus.Users.Domain.Entities;

public sealed class OutboxMessageEntity
{
  public Guid Id { get; set; }

  public string Topic { get; set; } = string.Empty;

  public string EventType { get; set; } = string.Empty;

  public string Payload { get; set; } = string.Empty;

  public string AggregateId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }

  public DateTime? ProcessedAt { get; set; }

  public string Status { get; set; } = "Pending";

  public int RetryCount { get; set; }

  public int MaxRetries { get; set; } = 3;

  public DateTime? NextRetryAt { get; set; }

  public string? Error { get; set; }

  public DateTime? LockedUntil { get; set; }

  public string? LockedBy { get; set; }

  public static class Statuses
  {
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
  }
}
