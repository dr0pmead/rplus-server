namespace RPlus.SDK.Audit.Enums;

#nullable enable
public enum AuditEventType
{
    Technical,
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
}
