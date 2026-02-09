// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Controllers.SyncUserDto
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using System;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

public record SyncUserDto
{
  public Guid UserId { get; init; }

  public string PhoneHash { get; init; }

  public bool IsActive { get; init; }

  public DateTime CreatedAt { get; init; }

  public SyncUserDto()
  {
  }
}
