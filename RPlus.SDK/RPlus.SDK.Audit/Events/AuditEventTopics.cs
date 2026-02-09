namespace RPlus.SDK.Audit.Events;

#nullable enable
public static class AuditEventTopics
{
    /// <summary>
    /// Kafka topic that the kernel audit service publishes to.
    /// </summary>
    public const string KernelAuditEvents = "kernel.audit.events";
}
