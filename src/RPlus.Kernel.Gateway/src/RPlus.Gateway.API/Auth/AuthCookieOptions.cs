using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;

namespace RPlus.Gateway.Api.Auth;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Gateway:AuthCookies";

    public string AccessTokenCookieName { get; init; } = "accessToken";
    public string RefreshTokenCookieName { get; init; } = "refreshToken";
    public string DeviceIdCookieName { get; init; } = "deviceId";

    /// <summary>
    /// Optional cookie domain. For multi-subdomain setups (e.g. api.* + app.*), set to ".example.com".
    /// </summary>
    public string? Domain { get; init; }

    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;
    public bool Secure { get; init; } = true;

    /// <summary>Default access cookie lifetime when upstream doesn't provide one.</summary>
    public int AccessMinutes { get; init; } = 15;

    /// <summary>Default refresh cookie lifetime when upstream doesn't provide one.</summary>
    public int RefreshDays { get; init; } = 30;

    public static AuthCookieOptions From(IConfiguration configuration)
    {
        var options = new AuthCookieOptions();
        configuration.GetSection(SectionName).Bind(options);
        return options;
    }
}
