// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.IIntegrationDbContext
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application;

public interface IIntegrationDbContext
{
  DbSet<IntegrationRoute> Routes { get; }

  DbSet<IntegrationPartner> Partners { get; }

  DbSet<IntegrationApiKey> ApiKeys { get; }

  DbSet<IntegrationAuditLog> AuditLogs { get; }

  DbSet<IntegrationListSyncConfig> ListSyncConfigs { get; }

  DbSet<IntegrationListSyncRun> ListSyncRuns { get; }

  // Double Entry Partner Scan System
  DbSet<PartnerScan> PartnerScans { get; }

  DbSet<PartnerCommit> PartnerCommits { get; }

  DbSet<TEntity> Set<TEntity>() where TEntity : class;

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
