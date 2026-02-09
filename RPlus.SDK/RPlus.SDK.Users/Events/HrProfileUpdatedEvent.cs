using System;

namespace RPlus.SDK.Users.Events;

/// <summary>
/// Event emitted when a user's HR profile is updated (FIO, avatar changes).
/// Published by HR Service after profile update.
/// Consumed by Integration Service to update scan cache.
/// </summary>
public record HrProfileUpdatedEvent
{
    /// <summary>
    /// User whose profile was updated.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// First name (имя).
    /// </summary>
    public required string FirstName { get; init; }

    /// <summary>
    /// Last name (фамилия).
    /// </summary>
    public required string LastName { get; init; }

    /// <summary>
    /// Middle name (отчество), optional.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// Avatar URL.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Timestamp of the update.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Kafka event name constant.
    /// </summary>
    public const string EventName = HrEventTopics.ProfileUpdated;
}
