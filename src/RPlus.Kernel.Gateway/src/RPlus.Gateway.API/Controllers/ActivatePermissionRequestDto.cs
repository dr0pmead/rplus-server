// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Api.Controllers.ActivatePermissionRequestDto
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 53C73046-40B0-469F-A259-3E029837F0C4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\ExecuteService.dll

#nullable enable
namespace RPlus.Gateway.Api.Controllers;

public record ActivatePermissionRequestDto(string PermissionId, bool Activate);


