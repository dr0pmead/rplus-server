using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RPlus.Documents.Api.Authentication;

public sealed class ServiceSecretAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISystemClock clock,
    IOptions<ServiceSecretAuthenticationOptions> secretOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder, clock)
{
    public const string SchemeName = "ServiceSecret";

    private readonly ServiceSecretAuthenticationOptions _secretOptions = secretOptions.Value ?? new ServiceSecretAuthenticationOptions();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(_secretOptions.HeaderName, out var headerValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var secret = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(secret) || !string.Equals(secret, _secretOptions.SharedSecret, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("invalid_service_secret"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "service"),
            new Claim("sub", "service"),
            new Claim("auth_type", "service_secret")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
