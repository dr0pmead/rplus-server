// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Queries.CheckAccess.CheckAccessQuery
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using MediatR;
using RPlus.Access.Application.Interfaces;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Application.Queries.CheckAccess;

public sealed record CheckAccessQuery(
  Guid UserId,
  string PermissionId,
  Guid? NodeId,
  Dictionary<string, object>? Context = null,
  bool UseStrongConsistency = false) : IRequest<PolicyDecision>, IBaseRequest
;
