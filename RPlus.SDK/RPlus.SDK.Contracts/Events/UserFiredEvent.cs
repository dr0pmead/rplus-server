// Decompiled with JetBrains decompiler
// Type: RPlus.Core.Contracts.Events.UserFiredEvent
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using System;

#nullable enable
namespace RPlus.Core.Contracts.Events;

public sealed class UserFiredEvent
{
  public Guid UserId { get; set; }

  public string Reason { get; set; } = string.Empty;

  public DateTime FiredAt { get; set; } = DateTime.UtcNow;
}
