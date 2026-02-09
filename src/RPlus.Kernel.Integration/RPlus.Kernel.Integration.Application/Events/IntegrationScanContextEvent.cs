using System;

namespace RPlus.Kernel.Integration.Application.Events;

public sealed record IntegrationScanContextEvent(
    Guid IntegrationId,
    string ContextId,
    string UserId,
    string EventType,
    string RequestId,
    DateTime CreatedAt)
{
    public const string EventName = "integration.scan.context.v1";
}
