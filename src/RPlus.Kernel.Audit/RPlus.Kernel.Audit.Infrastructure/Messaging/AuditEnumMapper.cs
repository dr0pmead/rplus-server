using DomainEventSource = RPlus.Kernel.Audit.Domain.ValueObjects.EventSource;
using DomainEventType = RPlus.Kernel.Audit.Domain.ValueObjects.AuditEventType;
using DomainEventSeverity = RPlus.Kernel.Audit.Domain.ValueObjects.EventSeverity;
using SdkEventSource = RPlus.SDK.Audit.Enums.EventSource;
using SdkEventType = RPlus.SDK.Audit.Enums.AuditEventType;
using SdkEventSeverity = RPlus.SDK.Audit.Enums.EventSeverity;

namespace RPlus.Kernel.Audit.Infrastructure.Messaging;

internal static class AuditEnumMapper
{
    public static SdkEventSource ToSdkSource(DomainEventSource source) =>
        source switch
        {
            DomainEventSource.Kernel => SdkEventSource.Kernel,
            DomainEventSource.Users => SdkEventSource.Users,
            DomainEventSource.Wallet => SdkEventSource.Module,
            DomainEventSource.Loyalty => SdkEventSource.Module,
            DomainEventSource.Guard => SdkEventSource.Module,
            DomainEventSource.External => SdkEventSource.System,
            DomainEventSource.System => SdkEventSource.System,
            DomainEventSource.Gateway => SdkEventSource.Gateway,
            DomainEventSource.Supervisor => SdkEventSource.Supervisor,
            DomainEventSource.Auth => SdkEventSource.Auth,
            DomainEventSource.Access => SdkEventSource.Access,
            DomainEventSource.Organization => SdkEventSource.Organization,
            DomainEventSource.Module => SdkEventSource.Module,
            _ => SdkEventSource.Module
        };

    public static DomainEventSource ToDomainSource(SdkEventSource source) =>
        source switch
        {
            SdkEventSource.Kernel => DomainEventSource.Kernel,
            SdkEventSource.Users => DomainEventSource.Users,
            SdkEventSource.Access => DomainEventSource.Access,
            SdkEventSource.Auth => DomainEventSource.Auth,
            SdkEventSource.Organization => DomainEventSource.Organization,
            SdkEventSource.Gateway => DomainEventSource.Gateway,
            SdkEventSource.Supervisor => DomainEventSource.Supervisor,
            SdkEventSource.Module => DomainEventSource.Module,
            SdkEventSource.System => DomainEventSource.System,
            _ => DomainEventSource.Unknown
        };

    public static SdkEventType ToSdkEventType(DomainEventType type) =>
        type switch
        {
            DomainEventType.SystemModeChanged => SdkEventType.SystemModeChanged,
            DomainEventType.LicenseExpiring => SdkEventType.LicenseExpiring,
            DomainEventType.LicenseExpired => SdkEventType.LicenseExpired,
            DomainEventType.SystemHealthStatus => SdkEventType.SystemHealthStatus,
            DomainEventType.RequestBlocked => SdkEventType.RequestBlocked,
            DomainEventType.AuthLogin => SdkEventType.AuthLogin,
            DomainEventType.AuthRefresh => SdkEventType.AuthRefresh,
            DomainEventType.WhitelistAccess => SdkEventType.WhitelistAccess,
            DomainEventType.ModuleStarted => SdkEventType.ModuleStarted,
            DomainEventType.ModuleStopped => SdkEventType.ModuleStopped,
            DomainEventType.ModuleRestarted => SdkEventType.ModuleRestarted,
            DomainEventType.ModuleFailed => SdkEventType.ModuleFailed,
            DomainEventType.ModuleException => SdkEventType.ModuleException,
            DomainEventType.UserCreated => SdkEventType.UserCreated,
            DomainEventType.UserUpdated => SdkEventType.UserUpdated,
            DomainEventType.UserDeleted => SdkEventType.UserDeleted,
            DomainEventType.AccessGranted => SdkEventType.AccessGranted,
            DomainEventType.AccessRevoked => SdkEventType.AccessRevoked,
            DomainEventType.OrganizationChanged => SdkEventType.OrganizationChanged,
            DomainEventType.Technical => SdkEventType.Technical,
            _ => SdkEventType.Technical
        };

    public static DomainEventType ToDomainEventType(SdkEventType type) =>
        type switch
        {
            SdkEventType.SystemModeChanged => DomainEventType.SystemModeChanged,
            SdkEventType.LicenseExpiring => DomainEventType.LicenseExpiring,
            SdkEventType.LicenseExpired => DomainEventType.LicenseExpired,
            SdkEventType.SystemHealthStatus => DomainEventType.SystemHealthStatus,
            SdkEventType.RequestBlocked => DomainEventType.RequestBlocked,
            SdkEventType.AuthLogin => DomainEventType.AuthLogin,
            SdkEventType.AuthRefresh => DomainEventType.AuthRefresh,
            SdkEventType.WhitelistAccess => DomainEventType.WhitelistAccess,
            SdkEventType.ModuleStarted => DomainEventType.ModuleStarted,
            SdkEventType.ModuleStopped => DomainEventType.ModuleStopped,
            SdkEventType.ModuleRestarted => DomainEventType.ModuleRestarted,
            SdkEventType.ModuleFailed => DomainEventType.ModuleFailed,
            SdkEventType.ModuleException => DomainEventType.ModuleException,
            SdkEventType.UserCreated => DomainEventType.UserCreated,
            SdkEventType.UserUpdated => DomainEventType.UserUpdated,
            SdkEventType.UserDeleted => DomainEventType.UserDeleted,
            SdkEventType.AccessGranted => DomainEventType.AccessGranted,
            SdkEventType.AccessRevoked => DomainEventType.AccessRevoked,
            SdkEventType.OrganizationChanged => DomainEventType.OrganizationChanged,
            SdkEventType.Technical => DomainEventType.Technical,
            _ => DomainEventType.Technical
        };

    public static SdkEventSeverity ToSdkSeverity(DomainEventSeverity severity) =>
        severity switch
        {
            DomainEventSeverity.Critical => SdkEventSeverity.Critical,
            DomainEventSeverity.Error => SdkEventSeverity.Error,
            DomainEventSeverity.Warning => SdkEventSeverity.Warning,
            DomainEventSeverity.Info => SdkEventSeverity.Info,
            DomainEventSeverity.Debug => SdkEventSeverity.Debug,
            _ => SdkEventSeverity.Info
        };

    public static DomainEventSeverity ToDomainSeverity(SdkEventSeverity severity) =>
        severity switch
        {
            SdkEventSeverity.Critical => DomainEventSeverity.Critical,
            SdkEventSeverity.Error => DomainEventSeverity.Error,
            SdkEventSeverity.Warning => DomainEventSeverity.Warning,
            SdkEventSeverity.Info or SdkEventSeverity.Information => DomainEventSeverity.Info,
            SdkEventSeverity.Debug => DomainEventSeverity.Debug,
            _ => DomainEventSeverity.Info
        };
}
