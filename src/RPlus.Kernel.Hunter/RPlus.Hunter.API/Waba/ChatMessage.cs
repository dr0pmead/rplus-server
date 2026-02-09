namespace RPlus.Hunter.API.Waba;

/// <summary>
/// Chat message entity — stores full conversation history between Hunter and candidates.
/// Supports both AI-auto and human-manual conversation modes.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Links to parsed_profiles.id.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>INBOUND (candidate → us) or OUTBOUND (us → candidate).</summary>
    public required string Direction { get; init; }

    /// <summary>AI, HUMAN, or CANDIDATE.</summary>
    public required string SenderType { get; init; }

    /// <summary>Message text content.</summary>
    public string? Content { get; set; }

    /// <summary>WhatsApp Business API message ID (from Meta).</summary>
    public string? WabaMessageId { get; set; }

    /// <summary>Delivery status: sent, delivered, read, failed.</summary>
    public string Status { get; set; } = "sent";

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Conversation mode for a candidate profile.
/// </summary>
public static class ConversationMode
{
    public const string AiAuto = "AI_AUTO";
    public const string HumanManual = "HUMAN_MANUAL";
}

/// <summary>
/// Message direction constants.
/// </summary>
public static class MessageDirection
{
    public const string Inbound = "INBOUND";
    public const string Outbound = "OUTBOUND";
}

/// <summary>
/// Sender type constants.
/// </summary>
public static class SenderType
{
    public const string Ai = "AI";
    public const string Human = "HUMAN";
    public const string Candidate = "CANDIDATE";
}
