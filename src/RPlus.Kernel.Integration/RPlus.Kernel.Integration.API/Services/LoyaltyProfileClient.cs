using System.Net;
using System.Net.Http.Json;

namespace RPlus.Kernel.Integration.Api.Services;

public interface ILoyaltyProfileClient
{
    Task<LoyaltyProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class LoyaltyProfileDto
{
    public Guid UserId { get; set; }
    public decimal PointsBalance { get; set; }
    public string? Level { get; set; }
    public string? LevelId { get; set; }
    public decimal Discount { get; set; }
    public decimal MotivationDiscount { get; set; }
    public decimal TotalDiscount { get; set; }
    
    /// <summary>
    /// v3.0: Current level index (1-based) from Loyalty.
    /// </summary>
    public int CurrentLevel { get; set; } = 1;
    
    /// <summary>
    /// v3.0: Total number of levels from Loyalty.
    /// </summary>
    public int TotalLevels { get; set; } = 1;
    
    /// <summary>
    /// [Deprecated] Computed Level (1-5) from Discount percentage.
    /// Use CurrentLevel instead for v3.0.
    /// </summary>
    public int LevelNumber => CurrentLevel > 0 ? CurrentLevel : ComputeLevelFromDiscount(Discount);
    
    private static int ComputeLevelFromDiscount(decimal discount)
    {
        if (discount <= 0) return 1;
        var level = (int)Math.Round(discount / 4m);
        return Math.Clamp(level, 1, 5);
    }
}

public sealed class LoyaltyProfileClient : ILoyaltyProfileClient
{
    private readonly HttpClient _httpClient;

    public LoyaltyProfileClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoyaltyProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync($"api/loyalty/profiles/{userId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoyaltyProfileDto>(cancellationToken: cancellationToken);
    }
}
