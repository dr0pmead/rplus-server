using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPlus.Hunter.API.HeadHunter;

/// <summary>
/// HeadHunter API client — search resumes, fetch details, open contacts.
/// Uses DelegatingHandler for auth and Polly for retry.
/// </summary>
public sealed class HeadHunterClient
{
    private readonly HttpClient _httpClient;
    private readonly HhOptions _options;
    private readonly ILogger<HeadHunterClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HeadHunterClient(
        HttpClient httpClient,
        IOptions<HhOptions> options,
        ILogger<HeadHunterClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Search resumes matching the given query.
    /// HH API endpoint: GET /resumes
    /// </summary>
    public async Task<HhSearchResult> SearchResumesAsync(
        string text,
        int? areaId = null,
        int? salaryFrom = null,
        int? salaryTo = null,
        int page = 0,
        int perPage = 20,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>
        {
            $"text={Uri.EscapeDataString(text)}",
            $"page={page}",
            $"per_page={Math.Min(perPage, 100)}"
        };

        if (areaId.HasValue)
            queryParams.Add($"area={areaId.Value}");

        if (salaryFrom.HasValue)
            queryParams.Add($"salary_from={salaryFrom.Value}");

        if (salaryTo.HasValue)
            queryParams.Add($"salary_to={salaryTo.Value}");

        // Only resumes updated in last 30 days
        queryParams.Add("order_by=relevance");

        var url = $"/resumes?{string.Join("&", queryParams)}";

        _logger.LogDebug("HH Search: {Url}", url);

        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<HhSearchResult>(JsonOptions, ct);
        return result ?? new HhSearchResult();
    }

    /// <summary>
    /// Fetch full resume details.
    /// HH API endpoint: GET /resumes/{resume_id}
    /// </summary>
    public async Task<HhResume?> GetResumeAsync(string resumeId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/resumes/{resumeId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("HH GetResume failed for {ResumeId}: {Status}", resumeId, response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<HhResume>(JsonOptions, ct);
    }

    /// <summary>
    /// Open contact details for a resume. THIS COSTS MONEY (HH API quota).
    /// Budget Fuse should be checked BEFORE calling this.
    /// HH API endpoint: POST /resumes/{resume_id}/negotiations
    /// </summary>
    public async Task<HhContactInfo?> OpenContactAsync(string resumeId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/resumes/{resumeId}?with_contact=true", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HH OpenContact failed for {ResumeId}: {Status}", resumeId, response.StatusCode);
                return null;
            }

            var resume = await response.Content.ReadFromJsonAsync<HhResume>(JsonOptions, ct);

            if (resume?.Contact is null)
            {
                _logger.LogWarning("HH Resume {ResumeId} has no contact info", resumeId);
                return null;
            }

            return resume.Contact;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HH OpenContact exception for {ResumeId}", resumeId);
            return null;
        }
    }

    /// <summary>
    /// Exchange authorization code for tokens (one-time OAuth setup).
    /// </summary>
    public async Task<HhTokenResponse?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        using var httpClient = new HttpClient();

        var formData = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri
        };

        var response = await httpClient.PostAsync(
            _options.TokenUrl,
            new FormUrlEncodedContent(formData),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("HH code exchange failed: {Status} {Error}", response.StatusCode, error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<HhTokenResponse>(ct);
    }
}

// ─── HH API Response Models ─────────────────────────────────────────────

public sealed class HhSearchResult
{
    public List<HhResumeShort> Items { get; set; } = new();
    public int Found { get; set; }
    public int Pages { get; set; }
    public int PerPage { get; set; }
    public int Page { get; set; }
}

public sealed class HhResumeShort
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public HhSalary? Salary { get; set; }
    public HhArea? Area { get; set; }
    public string? Age { get; set; }
    public HhExperience[]? Experience { get; set; }
    public string? UpdatedAt { get; set; }
    public string? CreatedAt { get; set; }
}

public sealed class HhResume
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Skills { get; set; }
    public HhSalary? Salary { get; set; }
    public HhArea? Area { get; set; }
    public HhExperience[]? Experience { get; set; }
    public HhEducation[]? Education { get; set; }
    public HhContactInfo? Contact { get; set; }
    public string? ResumeLocale { get; set; }
    public string? UpdatedAt { get; set; }

    /// <summary>
    /// Extracts a plain-text representation of the resume for AI scoring.
    /// </summary>
    public string ToPlainText()
    {
        var parts = new List<string>
        {
            $"Позиция: {Title}",
            Salary is not null ? $"ЗП: {Salary}" : "",
            Area is not null ? $"Город: {Area.Name}" : "",
            !string.IsNullOrEmpty(Skills) ? $"Навыки: {Skills}" : ""
        };

        if (Experience is { Length: > 0 })
        {
            parts.Add("Опыт работы:");
            foreach (var exp in Experience)
                parts.Add($"  - {exp.Company}: {exp.Position} ({exp.Start} - {exp.End ?? "наст. время"})");
        }

        if (Education is { Length: > 0 })
        {
            parts.Add("Образование:");
            foreach (var edu in Education)
                parts.Add($"  - {edu.Name}: {edu.Organization}");
        }

        return string.Join("\n", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}

public sealed class HhSalary
{
    public int? From { get; set; }
    public int? To { get; set; }
    public string? Currency { get; set; }

    public override string ToString() =>
        (From, To) switch
        {
            (not null, not null) => $"{From}-{To} {Currency}",
            (not null, null) => $"от {From} {Currency}",
            (null, not null) => $"до {To} {Currency}",
            _ => "не указана"
        };
}

public sealed class HhArea
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class HhExperience
{
    public string? Company { get; set; }
    public string? Position { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? Description { get; set; }
}

public sealed class HhEducation
{
    public string? Name { get; set; }
    public string? Organization { get; set; }
    public int? Year { get; set; }
}

public sealed class HhContactInfo
{
    public HhPhone[]? Phone { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Returns primary phone number or null.
    /// </summary>
    public string? GetPrimaryPhone()
    {
        if (Phone is not { Length: > 0 })
            return null;

        var primary = Phone[0];
        return $"+{primary.Country}{primary.City}{primary.Number}";
    }
}

public sealed class HhPhone
{
    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Number { get; set; }
}
