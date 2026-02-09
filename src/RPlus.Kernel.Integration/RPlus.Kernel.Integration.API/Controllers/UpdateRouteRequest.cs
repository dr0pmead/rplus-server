// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.UpdateRouteRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using System;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

public record UpdateRouteRequest(
  string? Name,
  string? RoutePattern,
  string? TargetHost,
  string? TargetService,
  string? TargetMethod,
  int? Priority,
  Guid? PartnerId,
  bool? IsActive)
;
