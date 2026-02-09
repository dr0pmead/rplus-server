namespace RPlus.SDK.Hunter.Models;

/// <summary>
/// Status of a sourcing task.
/// </summary>
public enum SourcingTaskStatus
{
    Active = 0,
    Paused = 1,
    PausedDailyLimit = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// Pipeline status for a parsed profile.
/// </summary>
public enum ProfileStatus
{
    New = 0,
    FilteredOk = 1,
    Rejected = 2,
    ContactOpened = 3,
    InviteSent = 4,
    Responded = 5,
    Qualified = 6
}

/// <summary>
/// Outreach channel for contacting a candidate.
/// </summary>
public enum OutreachChannel
{
    None = 0,
    WhatsApp = 1,
    Telegram = 2,
    Email = 3
}

/// <summary>
/// Core sourcing task model — SDK-first definition.
/// </summary>
public class SourcingTask
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Position title (e.g., "Java Senior Developer").</summary>
    public required string PositionName { get; init; }

    /// <summary>Search keywords for HH API (e.g., "Java Spring Remote").</summary>
    public required string SearchQuery { get; init; }

    /// <summary>AI screening conditions (e.g., "Опыт > 3 лет, ЗП < 1.5 млн").</summary>
    public required string Conditions { get; init; }

    /// <summary>Outreach message template. Supports {position_name}, {company_name} placeholders.</summary>
    public string? MessageTemplate { get; init; }

    /// <summary>Safety guard: max contacts to open per day. Prevents budget burn.</summary>
    public int DailyContactLimit { get; init; } = 50;

    /// <summary>Minimum AI score to pass filtering (0-100).</summary>
    public int MinScore { get; init; } = 70;

    public SourcingTaskStatus Status { get; set; } = SourcingTaskStatus.Active;
    public int CandidatesFound { get; set; }
    public int CandidatesContacted { get; set; }
    public int CandidatesResponded { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>User ID of the HR who created this task.</summary>
    public Guid CreatedByUserId { get; init; }
}

/// <summary>
/// Parsed candidate profile from external source (HH, LinkedIn).
/// </summary>
public class ParsedProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TaskId { get; init; }

    /// <summary>External resume ID (e.g., HH resume ID).</summary>
    public required string ExternalId { get; init; }

    /// <summary>Source platform identifier.</summary>
    public string Source { get; init; } = "hh.ru";

    /// <summary>Full text of the resume for AI analysis.</summary>
    public required string RawData { get; set; }

    /// <summary>SHA256 hash of RawData for smart dedup.</summary>
    public required string ContentHash { get; set; }

    /// <summary>AI relevance score (0-100).</summary>
    public int? AiScore { get; set; }

    /// <summary>AI verdict text (e.g., "Подходит, опыт 5 лет").</summary>
    public string? AiVerdict { get; set; }

    /// <summary>Phone number (null until contact opened).</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Telegram handle (extracted from raw_data if present).</summary>
    public string? TelegramHandle { get; set; }

    /// <summary>Email (if available).</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Preferred outreach channel (auto-detected).</summary>
    public OutreachChannel PreferredChannel { get; set; } = OutreachChannel.None;

    /// <summary>Conversation mode: AI_AUTO or HUMAN_MANUAL.</summary>
    public string ConversationMode { get; set; } = "AI_AUTO";

    public ProfileStatus Status { get; set; } = ProfileStatus.New;
    public DateTime ParsedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ContactedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

