// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Persistence.RPlusDbContext
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.EntityFrameworkCore;

#nullable enable
namespace RPlus.SDK.Infrastructure.Persistence;

public abstract class RPlusDbContext : DbContext
{
  public RPlusDbContext(DbContextOptions options)
    : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
  }
}
