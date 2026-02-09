// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Infrastructure.Persistence.AccessDbContext
// Assembly: RPlus.Access.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: EAF2AFCF-7B6C-4BF5-806A-4B3105E7710C
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Domain.Entities;
using System.Reflection;

#nullable enable
namespace RPlus.Access.Infrastructure.Persistence;

public class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext((DbContextOptions) options), IAccessDbContext
{
  public DbSet<App> Apps => this.Set<App>();

  public DbSet<Permission> Permissions => this.Set<Permission>();

  public DbSet<Role> Roles => this.Set<Role>();

  public DbSet<AccessPolicy> AccessPolicies => this.Set<AccessPolicy>();

  public DbSet<PolicyAssignment> PolicyAssignments => this.Set<PolicyAssignment>();

  public DbSet<LocalUserAssignment> UserAssignments => this.Set<LocalUserAssignment>();

  public DbSet<EffectiveSnapshot> EffectiveSnapshots => this.Set<EffectiveSnapshot>();

  public DbSet<SodPolicySet> SodPolicySets => this.Set<SodPolicySet>();

  public DbSet<SodPolicy> SodPolicies => this.Set<SodPolicy>();

  public DbSet<ServiceRegistryEntry> ServiceRegistry => this.Set<ServiceRegistryEntry>();

  public DbSet<RootRegistryEntry> RootRegistry => this.Set<RootRegistryEntry>();

  public DbSet<IntegrationApiKeyPermission> IntegrationApiKeyPermissions
  {
    get => this.Set<IntegrationApiKeyPermission>();
  }

  public DbSet<IntegrationApiKeyRecord> IntegrationApiKeys => this.Set<IntegrationApiKeyRecord>();

  public DbSet<PartnerUserLink> PartnerUserLinks => this.Set<PartnerUserLink>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.HasPostgresExtension("ltree");
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    modelBuilder.HasDefaultSchema("access");
  }
}
