// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Contracts.Events.UserStatusChangedEvent
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using System;

#nullable enable
namespace RPlus.Auth.Contracts.Events;

public sealed record UserStatusChangedEvent
{
  public Guid UserId { get; init; }

  public string Status { get; init; } = string.Empty;

  public string Reason { get; init; } = string.Empty;

  public DateTime ChangedAt { get; init; }
}
