// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Domain.Events.GuardEvent
// Assembly: RPlus.Kernel.Guard.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F72AD90D-A3D7-4AD8-8D13-E2C0626F5502
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Domain.dll

using System;

#nullable enable
namespace RPlus.Kernel.Guard.Domain.Events;

public abstract class GuardEvent
{
  public DateTime OccurredAt { get; } = DateTime.UtcNow;

  public string TraceId { get; } = Guid.NewGuid().ToString();
}
