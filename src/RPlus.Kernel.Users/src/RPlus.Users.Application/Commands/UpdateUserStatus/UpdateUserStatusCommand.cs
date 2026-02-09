// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Application.Commands.UpdateUserStatus.UpdateUserStatusCommand
// Assembly: RPlus.Users.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 48B001A8-2E15-4980-831E-0027ECCC6407
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Application.dll

using MediatR;
using RPlus.SDK.Users.Enums;
using System;

#nullable enable
namespace RPlus.Users.Application.Commands.UpdateUserStatus;

public record UpdateUserStatusCommand(Guid UserId, UserStatus Status) : IRequest, IBaseRequest;
