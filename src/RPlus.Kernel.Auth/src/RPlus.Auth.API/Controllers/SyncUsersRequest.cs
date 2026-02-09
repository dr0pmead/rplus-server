// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Controllers.SyncUsersRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

public record SyncUsersRequest
{
  public List<SyncUserDto> Users { get; init; }

  public SyncUsersRequest()
  {
  }
}
