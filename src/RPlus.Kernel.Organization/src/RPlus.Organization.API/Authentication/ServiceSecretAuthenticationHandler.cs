using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace RPlus.Organization.Api.Authentication;

public sealed class ServiceSecretAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ServiceSecret";

    private readonly IOptionsMonitor<ServiceSecretAuthenticationOptions> _options;

    public ServiceSecretAuthenticationHandler(
        IOptionsMonitor<ServiceSecretAuthenticationOptions> options,
        IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(schemeOptions, logger, encoder)
    {
        _options = options;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = (_options.CurrentValue.SharedSecret ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue("x-rplus-service-secret", out var actualValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var actual = actualValues.ToString().Trim();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid service secret"));
        }

        var claims = new List<Claim>
        {
            new("sub", "service:organization"),
            new(ClaimTypes.NameIdentifier, "service:organization"),
        };

        if (Request.Headers.TryGetValue("x-tenant-id", out var tenantValues))
        {
            var tenant = tenantValues.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                claims.Add(new Claim("tenant_id", tenant));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

