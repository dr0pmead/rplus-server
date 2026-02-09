// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Messaging.Events.UserCreated
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Messaging.Events;

public record UserCreated(
  Guid EventId,
  DateTime OccurredAt,
  Guid UserId,
  Guid TenantId,
  string Login,
  string Email,
  string Phone,
  string Source,
  string UserType = "Platform",
  string? FirstName = null,
  string? LastName = null,
  string? MiddleName = null,
  System.Collections.Generic.Dictionary<string, string>? Properties = null)
;
