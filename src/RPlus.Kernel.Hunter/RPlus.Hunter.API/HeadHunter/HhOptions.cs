namespace RPlus.Hunter.API.HeadHunter;

/// <summary>
/// Configuration for HeadHunter API integration.
/// </summary>
public sealed class HhOptions
{
    public const string SectionName = "HeadHunter";

    /// <summary>OAuth2 Client ID from dev.hh.ru.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 Client Secret from dev.hh.ru.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth2 Redirect URI (must match dev.hh.ru app settings).</summary>
    public string RedirectUri { get; set; } = "http://localhost:5040/api/v1/hunter/hh/callback";

    /// <summary>HH API base URL.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.hh.ru";

    /// <summary>HH OAuth token URL.</summary>
    public string TokenUrl { get; set; } = "https://hh.ru/oauth/token";

    /// <summary>HH OAuth authorize URL.</summary>
    public string AuthorizeUrl { get; set; } = "https://hh.ru/oauth/authorize";

    /// <summary>Contact email for User-Agent header. CRITICAL: HH bans without it.</summary>
    public string ContactEmail { get; set; } = "admin@rplus.kz";

    /// <summary>Max requests per second to HH API. Conservative default.</summary>
    public int MaxRequestsPerSecond { get; set; } = 2;

    /// <summary>Default search area IDs (HH region codes).</summary>
    public Dictionary<string, int> AreaCodes { get; set; } = new()
    {
        ["Алматы"] = 40,
        ["Астана"] = 159,
        ["Казахстан"] = 40, // fallback
        ["Москва"] = 1,
        ["Санкт-Петербург"] = 2,
        ["Вся Россия"] = 113
    };
}
