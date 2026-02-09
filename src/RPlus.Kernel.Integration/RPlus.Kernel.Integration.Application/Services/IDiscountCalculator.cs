namespace RPlus.Kernel.Integration.Application.Services;

/// <summary>
/// Calculator for dynamic level-based discounts.
/// v3.0: Integration is a "dumb" calculator - takes RPlusDiscount from cache.
/// </summary>
public interface IDiscountCalculator
{
    DiscountResult Calculate(CachedUserProfile user, PartnerDiscountConfig partner);
}

/// <summary>
/// v3.0: Cached user profile from Redis with CurrentLevel, TotalLevels, RPlusDiscount.
/// </summary>
public record CachedUserProfile(
    int CurrentLevel,
    int TotalLevels,
    decimal RPlusDiscount);

/// <summary>
/// Partner discount configuration extracted from IntegrationPartner.
/// </summary>
public record PartnerDiscountConfig(
    string DiscountStrategy,
    string PartnerCategory,
    decimal? MaxDiscount,
    decimal? FixedDiscount,
    string? HappyHoursConfigJson);

/// <summary>
/// v3.0: Result of discount calculation.
/// Clean response with rplus, partner, total.
/// </summary>
public record DiscountResult(
    decimal RPlusDiscount,
    decimal PartnerDiscount,
    decimal TotalDiscount,
    int EffectiveLevel,
    bool IsHappyHour);
