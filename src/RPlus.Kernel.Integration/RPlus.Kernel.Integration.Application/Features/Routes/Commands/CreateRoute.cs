// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Features.Routes.Commands.CreateRouteCommand
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using MediatR;
using System;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Features.Routes.Commands;

public record CreateRouteCommand(
  string Name,
  string RoutePattern,
  string TargetHost,
  string TargetService,
  string TargetMethod,
  Guid? PartnerId = null,
  int Priority = 0) : IRequest<Guid>, IBaseRequest
;
