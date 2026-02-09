using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IHrProfileClient
{
    Task<HrProfileDto?> GetBasicAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class HrProfileDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public Guid? PhotoFileId { get; set; }
    public string? AvatarUrl { get; set; }
}

public sealed class HrProfileClient : IHrProfileClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<IntegrationHrOptions> _options;
    private readonly ILogger<HrProfileClient> _logger;

    public HrProfileClient(HttpClient httpClient, IOptionsMonitor<IntegrationHrOptions> options, ILogger<HrProfileClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<HrProfileDto?> GetBasicAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            return null;

        var secret = (_options.CurrentValue.SharedSecret ?? string.Empty).Trim();
        _httpClient.DefaultRequestHeaders.Remove("x-rplus-service-secret");
        if (!string.IsNullOrWhiteSpace(secret))
        {
            _httpClient.DefaultRequestHeaders.Add("x-rplus-service-secret", secret);
        }

        using var response = await _httpClient.GetAsync($"api/hr/profiles/{userId:D}/basic", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("HR basic profile request failed ({Status}): {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<HrProfileDto>(cancellationToken: cancellationToken);
    }
}
