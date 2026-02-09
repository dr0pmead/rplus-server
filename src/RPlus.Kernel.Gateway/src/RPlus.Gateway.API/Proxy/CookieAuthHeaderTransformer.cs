using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using RPlus.Gateway.Api.Auth;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace RPlus.Gateway.Api.Proxy;

public sealed class CookieAuthHeaderTransformer : HttpTransformer
{
    private readonly AuthCookieOptions _cookies;
    private readonly string? _documentsBase;
    private readonly string? _internalSecret;

    public CookieAuthHeaderTransformer(AuthCookieOptions cookies, string? documentsBase, string? internalSecret)
    {
        _cookies = cookies;
        _documentsBase = string.IsNullOrWhiteSpace(documentsBase) ? null : documentsBase.TrimEnd('/');
        _internalSecret = string.IsNullOrWhiteSpace(internalSecret) ? null : internalSecret;
    }

    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await HttpTransformer.Default.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken)
            .ConfigureAwait(false);

        if (proxyRequest.Headers.Contains(HeaderNames.Authorization))
            return;

        if (httpContext.Request.Cookies.TryGetValue(_cookies.AccessTokenCookieName, out var token) &&
            !string.IsNullOrWhiteSpace(token))
        {
            proxyRequest.Headers.TryAddWithoutValidation(HeaderNames.Authorization, $"Bearer {token}");
            return;
        }

        if (httpContext.Request.Cookies.TryGetValue("access_token", out var legacyToken) &&
            !string.IsNullOrWhiteSpace(legacyToken))
        {
            proxyRequest.Headers.TryAddWithoutValidation(HeaderNames.Authorization, $"Bearer {legacyToken}");
        }

        if (!string.IsNullOrWhiteSpace(_internalSecret) &&
            !string.IsNullOrWhiteSpace(_documentsBase) &&
            destinationPrefix.TrimEnd('/').Equals(_documentsBase, System.StringComparison.OrdinalIgnoreCase))
        {
            proxyRequest.Headers.TryAddWithoutValidation("X-RPlus-Internal", _internalSecret);
        }
    }
}
