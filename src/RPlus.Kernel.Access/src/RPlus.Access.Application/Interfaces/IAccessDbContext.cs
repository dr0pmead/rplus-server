// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.Interfaces.IAccessDbContext
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using Microsoft.EntityFrameworkCore;
using RPlus.Access.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Access.Application.Interfaces;

public interface IAccessDbContext
{
  DbSet<App> Apps { get; }

  DbSet<Permission> Permissions { get; }

  DbSet<Role> Roles { get; }

  DbSet<AccessPolicy> AccessPolicies { get; }

  DbSet<PolicyAssignment> PolicyAssignments { get; }

  DbSet<LocalUserAssignment> UserAssignments { get; }

  DbSet<EffectiveSnapshot> EffectiveSnapshots { get; }

  DbSet<SodPolicySet> SodPolicySets { get; }

  DbSet<SodPolicy> SodPolicies { get; }

  DbSet<ServiceRegistryEntry> ServiceRegistry { get; }

  DbSet<RootRegistryEntry> RootRegistry { get; }

  DbSet<IntegrationApiKeyPermission> IntegrationApiKeyPermissions { get; }

  DbSet<IntegrationApiKeyRecord> IntegrationApiKeys { get; }

  DbSet<PartnerUserLink> PartnerUserLinks { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
