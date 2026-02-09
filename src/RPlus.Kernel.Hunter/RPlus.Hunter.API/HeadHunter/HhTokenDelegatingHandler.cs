using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Hunter.API.Persistence;
using System.Net;
using System.Net.Http.Headers;

namespace RPlus.Hunter.API.HeadHunter;

/// <summary>
/// HttpClient DelegatingHandler that automatically refreshes HH OAuth2 tokens.
/// On 401 Unauthorized: reads current refresh_token from DB → calls /oauth/token → updates DB → retries request.
/// </summary>
public sealed class HhTokenDelegatingHandler : DelegatingHandler
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly HhOptions _options;
    private readonly ILogger<HhTokenDelegatingHandler> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public HhTokenDelegatingHandler(
        IDbContextFactory<HunterDbContext> dbFactory,
        IOptions<HhOptions> options,
        ILogger<HhTokenDelegatingHandler> logger)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Attach current access token
        var credential = await GetCredentialAsync(ct);
        if (credential is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);

        var response = await base.SendAsync(request, ct);

        // If 401 → try refresh once
        if (response.StatusCode == HttpStatusCode.Unauthorized && credential is not null)
        {
            _logger.LogWarning("HH API returned 401 — attempting token refresh");

            var refreshed = await TryRefreshTokenAsync(credential.RefreshToken, ct);
            if (refreshed)
            {
                // Clone request with new token (original request is already sent)
                var retryRequest = await CloneRequestAsync(request, ct);
                var newCred = await GetCredentialAsync(ct);
                if (newCred is not null)
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newCred.AccessToken);

                response.Dispose();
                response = await base.SendAsync(retryRequest, ct);
            }
        }

        return response;
    }

    private async Task<HhCredential?> GetCredentialAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.HhCredentials
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> TryRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        // Prevent concurrent refresh storms
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10), ct))
            return false;

        try
        {
            using var httpClient = new HttpClient();

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            };

            var tokenResponse = await httpClient.PostAsync(
                _options.TokenUrl,
                new FormUrlEncodedContent(formData),
                ct);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var error = await tokenResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("HH token refresh failed: {StatusCode} {Error}",
                    tokenResponse.StatusCode, error);
                return false;
            }

            var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<HhTokenResponse>(ct);
            if (tokenJson is null)
                return false;

            // Update DB
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var existing = await db.HhCredentials.OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                existing.AccessToken = tokenJson.AccessToken;
                existing.RefreshToken = tokenJson.RefreshToken;
                existing.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn);
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.HhCredentials.Add(new HhCredential
                {
                    AccessToken = tokenJson.AccessToken,
                    RefreshToken = tokenJson.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn)
                });
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("HH token refreshed successfully, expires at {ExpiresAt}",
                DateTime.UtcNow.AddSeconds(tokenJson.ExpiresIn));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HH token refresh exception");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content is not null)
        {
            var content = await original.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(content);
            if (original.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }

        return clone;
    }
}

/// <summary>
/// HH OAuth token response DTO.
/// </summary>
public sealed record HhTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string TokenType { get; init; } = "bearer";
}
