// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.AccessDbContextFactory
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

#nullable enable
namespace RPlus.Access.Infrastructure.Persistence;

public class AccessDbContextFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
  public AccessDbContext CreateDbContext(string[] args)
  {
    IConfigurationRoot configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Development.json", true).Build();
    DbContextOptionsBuilder<AccessDbContext> optionsBuilder = new DbContextOptionsBuilder<AccessDbContext>();
    string connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Port=5432;Database=rplus_access;User Id=postgres;Password=postgres;";
    optionsBuilder.UseNpgsql<AccessDbContext>(connectionString);
    return new AccessDbContext(optionsBuilder.Options);
  }
}
