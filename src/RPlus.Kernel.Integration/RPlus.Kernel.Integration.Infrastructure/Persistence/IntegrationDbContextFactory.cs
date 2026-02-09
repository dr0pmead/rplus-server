// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Persistence.IntegrationDbContextFactory
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Persistence;

public class IntegrationDbContextFactory : IDesignTimeDbContextFactory<IntegrationDbContext>
{
  public IntegrationDbContext CreateDbContext(string[] args)
  {
    DbContextOptionsBuilder<IntegrationDbContext> optionsBuilder = new DbContextOptionsBuilder<IntegrationDbContext>();
    optionsBuilder.UseNpgsql<IntegrationDbContext>("Host=localhost;Database=rplus_access;Username=postgres;Password=postgres");
    return new IntegrationDbContext(optionsBuilder.Options);
  }
}
