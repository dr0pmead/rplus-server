namespace RPlus.Hunter.API.Waba;

/// <summary>
/// Configuration for direct Meta Cloud API (Graph API v21.0).
/// No intermediary — we talk to Meta directly.
/// </summary>
public sealed class WabaOptions
{
    public const string SectionName = "Waba";

    /// <summary>
    /// Permanent or Temporary Access Token from Meta Developers Portal.
    /// Starts with "EAAV..." or "EAAG..."
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// The Phone Number ID (NOT the phone number itself).
    /// Found in Meta Business Manager → API Setup → Step 1.
    /// </summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>
    /// App Secret from Meta App settings.
    /// Used for HMAC-SHA256 webhook signature validation (X-Hub-Signature-256).
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary string to verify webhook subscription (hub.verify_token handshake).
    /// </summary>
    public string WebhookVerifyToken { get; set; } = "hunter_secret";

    /// <summary>
    /// Default template name for first-contact invites.
    /// Must be pre-approved in Meta Business Manager.
    /// Use "hello_world" for testing.
    /// </summary>
    public string InviteTemplateName { get; set; } = "hello_world";

    /// <summary>
    /// Language code for templates (e.g. "ru", "en_US").
    /// </summary>
    public string DefaultLanguage { get; set; } = "ru";
}
