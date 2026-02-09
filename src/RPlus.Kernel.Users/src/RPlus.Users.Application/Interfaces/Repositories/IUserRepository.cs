// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Application.Interfaces.Repositories.IUserRepository
// Assembly: RPlus.Users.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 48B001A8-2E15-4980-831E-0027ECCC6407
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Application.dll

using RPlus.Users.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Users.Application.Interfaces.Repositories;

public interface IUserRepository
{
  Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct = default (CancellationToken));

  Task AddAsync(UserEntity user, CancellationToken ct = default (CancellationToken));

  Task UpdateAsync(UserEntity user, CancellationToken ct = default (CancellationToken));
}
