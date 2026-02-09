// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Infrastructure.Persistence.OrganizationDbContextFactory
// Assembly: RPlus.Organization.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67956CC4-52BB-48F3-9302-33FB247F5EB1
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System;
using System.IO;

#nullable enable
namespace RPlus.Organization.Infrastructure.Persistence;

public class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
  public OrganizationDbContext CreateDbContext(string[] args)
  {
    IConfigurationRoot configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Development.json", true).Build();
    DbContextOptionsBuilder<OrganizationDbContext> optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
    string connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Port=5432;Database=rplus_organization;User Id=postgres;Password=postgres;";
    ((DbContextOptionsBuilder<OrganizationDbContext>) optionsBuilder).UseNpgsql<OrganizationDbContext>(connectionString, (Action<NpgsqlDbContextOptionsBuilder>) (o => { }));
    return new OrganizationDbContext(optionsBuilder.Options);
  }
}
