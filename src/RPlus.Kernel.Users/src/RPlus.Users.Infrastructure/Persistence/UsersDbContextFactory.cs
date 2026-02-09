// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Infrastructure.Persistence.UsersDbContextFactory
// Assembly: RPlus.Users.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9CF06FE7-40AC-4ED9-B2CD-559A2CFCED24
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

#nullable enable
namespace RPlus.Users.Infrastructure.Persistence;

public class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
  public UsersDbContext CreateDbContext(string[] args)
  {
    DbContextOptionsBuilder<UsersDbContext> optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
    optionsBuilder.UseNpgsql<UsersDbContext>("Host=localhost;Database=users;Username=postgres;Password=postgres");
    return new UsersDbContext(optionsBuilder.Options);
  }
}
