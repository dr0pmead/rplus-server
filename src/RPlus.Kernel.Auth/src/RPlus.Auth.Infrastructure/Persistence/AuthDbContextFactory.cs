// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Persistence.AuthDbContextFactory
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

#nullable enable
namespace RPlus.Auth.Infrastructure.Persistence;

public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
  public AuthDbContext CreateDbContext(string[] args)
  {
    string str = Path.Combine(Directory.GetCurrentDirectory(), "RPlus.Auth");
    if (!Directory.Exists(str))
      str = Directory.GetCurrentDirectory();
    IConfigurationRoot configuration = EnvironmentVariablesExtensions.AddEnvironmentVariables(new ConfigurationBuilder().SetBasePath(str).AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Development.json", true)).Build();
    DbContextOptionsBuilder<AuthDbContext> optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
    string connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Host=postgres;Database=rplus_auth;Username=postgres;Password=postgres";
    optionsBuilder.UseNpgsql<AuthDbContext>(connectionString);
    return new AuthDbContext(optionsBuilder.Options);
  }
}
