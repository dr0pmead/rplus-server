// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Services.RootTableInitializer
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RPlus.Access.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Infrastructure.Services;

public class RootTableInitializer : IHostedService
{
  private readonly IServiceProvider _serviceProvider;

  public RootTableInitializer(IServiceProvider serviceProvider)
  {
    this._serviceProvider = serviceProvider;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    using (IServiceScope scope = this._serviceProvider.CreateScope())
    {
      int num = await ((DbContext) scope.ServiceProvider.GetRequiredService<IAccessDbContext>()).Database.ExecuteSqlRawAsync("\r\n            CREATE SCHEMA IF NOT EXISTS access;\r\n            CREATE TABLE IF NOT EXISTS access.root_registry (\r\n                \"HashedUserId\" text NOT NULL,\r\n                \"CreatedAt\" timestamp with time zone NOT NULL,\r\n                \"Status\" text NOT NULL DEFAULT 'ACTIVE',\r\n                CONSTRAINT \"PK_root_registry\" PRIMARY KEY (\"HashedUserId\")\r\n            );\r\n        ", cancellationToken);
    }
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
