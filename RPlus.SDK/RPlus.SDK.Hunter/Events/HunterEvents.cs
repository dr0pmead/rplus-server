namespace RPlus.SDK.Hunter.Events;

/// <summary>
/// Kafka event topic constants for Hunter domain.
/// </summary>
public static class HunterTopics
{
    public const string TaskCreated = "recruitment.task.created.v1";
    public const string TaskUpdated = "recruitment.task.updated.v1";
    public const string ProfilesParsed = "recruitment.profiles.parsed.v1";
    public const string ProfilesScored = "recruitment.profiles.scored.v1";
    public const string ContactOpened = "recruitment.contact.opened.v1";
    public const string OutreachSent = "recruitment.outreach.sent.v1";
}

/// <summary>
/// Event: New sourcing task created by HR.
/// </summary>
public sealed record TaskCreatedEvent
{
    public required Guid TaskId { get; init; }
    public required string PositionName { get; init; }
    public required string SearchQuery { get; init; }
    public required string Conditions { get; init; }
    public required int MinScore { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event: Profiles parsed from external source, ready for AI scoring.
/// </summary>
public sealed record ProfileParsedEvent
{
    public required Guid ProfileId { get; init; }
    public required Guid TaskId { get; init; }
    public required string ExternalId { get; init; }
    public required string RawData { get; init; }
    public required string ContentHash { get; init; }
    public required string Conditions { get; init; }
    public required int MinScore { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event: AI scored a profile, ready for contact opening.
/// </summary>
public sealed record ProfileScoredEvent
{
    public required Guid ProfileId { get; init; }
    public required Guid TaskId { get; init; }
    public required int Score { get; init; }
    public required string Verdict { get; init; }
    public required bool Passed { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event: Contact opened for a candidate (HH API call made, budget spent).
/// </summary>
public sealed record ContactOpenedEvent
{
    public required Guid ProfileId { get; init; }
    public required Guid TaskId { get; init; }
    public required string Phone { get; init; }
    public string? TelegramHandle { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event: Outreach message sent to candidate.
/// </summary>
public sealed record OutreachSentEvent
{
    public required Guid ProfileId { get; init; }
    public required Guid TaskId { get; init; }
    public required string Channel { get; init; }
    public required string MessageId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
