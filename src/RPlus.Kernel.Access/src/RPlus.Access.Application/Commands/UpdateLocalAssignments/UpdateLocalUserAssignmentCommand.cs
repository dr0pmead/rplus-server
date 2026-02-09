// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Commands.UpdateLocalAssignments.UpdateLocalUserAssignmentCommand
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using MediatR;
using System;

#nullable enable
namespace RPlus.Access.Application.Commands.UpdateLocalAssignments;

public record UpdateLocalUserAssignmentCommand(
  Guid UserId,
  Guid NodeId,
  string RoleCode,
  string PathSnapshot) : IRequest, IBaseRequest
;
