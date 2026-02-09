namespace RPlus.SDK.Users.Events;

/// <summary>
/// Canonical Kafka topic names for HR/User-related events.
/// </summary>
public static class HrEventTopics
{
    /// <summary>
    /// Topic emitted when a user's HR profile is updated (FIO, avatar).
    /// Consumed by Integration service to update scan cache.
    /// </summary>
    public const string ProfileUpdated = "hr.profile.updated.v1";
}
