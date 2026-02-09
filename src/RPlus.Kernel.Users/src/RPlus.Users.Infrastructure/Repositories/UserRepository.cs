// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Repositories.UserRepository
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using RPlus.Users.Application.Interfaces.Repositories;
using RPlus.Users.Domain.Entities;
using RPlus.Users.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Users.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
  private readonly UsersDbContext _db;

  public UserRepository(UsersDbContext db) => this._db = db;

  public async Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken ct = default (CancellationToken))
  {
    return await this._db.Users.FindAsync(new object[1]
    {
      (object) id
    }, ct);
  }

  public async Task AddAsync(UserEntity user, CancellationToken ct = default (CancellationToken))
  {
    this._db.Users.Add(user);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task UpdateAsync(UserEntity user, CancellationToken ct = default (CancellationToken))
  {
    this._db.Users.Update(user);
    int num = await this._db.SaveChangesAsync(ct);
  }
}
