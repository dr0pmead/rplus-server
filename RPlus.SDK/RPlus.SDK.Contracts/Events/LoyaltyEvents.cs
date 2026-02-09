// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Contracts.Events.LoyaltyProfileCreated
// Assembly: RPlus.SDK.Contracts, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: A6C08EAE-EAE1-417A-A2D9-4C69FE3F3790
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Contracts.dll

using System;

#nullable enable
namespace RPlus.SDK.Contracts.Events;

public record LoyaltyProfileCreated(
  string ProfileId,
  string UserId,
  int InitialLevel,
  DateTime CreatedAt)
;
