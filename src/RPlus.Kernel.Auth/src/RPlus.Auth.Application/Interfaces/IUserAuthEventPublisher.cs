// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.IUserAuthEventPublisher
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using RPlus.Auth.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface IUserAuthEventPublisher
{
  Task PublishUserAuthUpdatedAsync(
    Guid userId,
    string? ipAddress,
    CancellationToken cancellationToken);

  Task PublishUserCreatedAsync(
    AuthUserEntity user,
    string? firstName,
    string? lastName,
    string? middleName,
    System.Collections.Generic.Dictionary<string, string>? properties,
    CancellationToken ct);

  Task PublishUserTerminatedAsync(
    Guid userId,
    string reason,
    CancellationToken ct);
}
