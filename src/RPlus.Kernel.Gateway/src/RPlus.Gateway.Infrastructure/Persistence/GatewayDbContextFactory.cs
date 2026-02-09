// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Infrastructure.Persistence.GatewayDbContextFactory
// Assembly: RPlus.Gateway.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 54ABDD44-3C89-45DC-858E-4ECA8F349EB2
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using RPlus.Gateway.Persistence;

#nullable enable
namespace RPlus.Gateway.Infrastructure.Persistence;

public class GatewayDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
  public GatewayDbContext CreateDbContext(string[] args)
  {
    DbContextOptionsBuilder<GatewayDbContext> optionsBuilder = new DbContextOptionsBuilder<GatewayDbContext>();
    optionsBuilder.UseNpgsql<GatewayDbContext>("Host=localhost;Database=rplus_gateway;Username=postgres;Password=postgres");
    return new GatewayDbContext(optionsBuilder.Options);
  }
}
