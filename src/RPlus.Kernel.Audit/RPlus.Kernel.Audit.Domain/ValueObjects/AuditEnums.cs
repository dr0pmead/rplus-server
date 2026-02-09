// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Domain.ValueObjects.AuditEventType
// Assembly: RPlus.Kernel.Audit.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 005C004C-7DDA-4A11-A8F2-5AF64ACE33B4
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Domain.dll

#nullable disable
namespace RPlus.Kernel.Audit.Domain.ValueObjects;

public enum AuditEventType
{
  SystemModeChanged,
  LicenseExpiring,
  LicenseExpired,
  SystemHealthStatus,
  RequestBlocked,
  AuthLogin,
  AuthRefresh,
  WhitelistAccess,
  ModuleStarted,
  ModuleStopped,
  ModuleRestarted,
  ModuleFailed,
  ModuleException,
  UserCreated,
  UserUpdated,
  UserDeleted,
  AccessGranted,
  AccessRevoked,
  OrganizationChanged,
  Technical,
}

public enum EventSource
{
  Kernel,
  Integration,
  Users,
  Wallet,
  Loyalty,
  Guard,
  External,
  Unknown,
  System,
  Gateway,
  Supervisor,
  Auth,
  Access,
  Organization,
  Module
}

public enum EventSeverity
{
  Critical,
  Error,
  Warning,
  Info,
  Debug
}
