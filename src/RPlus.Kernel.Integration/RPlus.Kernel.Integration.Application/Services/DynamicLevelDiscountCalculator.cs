using System;
using RPlus.Kernel.Integration.Domain.ValueObjects;

namespace RPlus.Kernel.Integration.Application.Services;

/// <summary>
/// v3.0: Dynamic Scalable Discount Calculator.
/// Integration is a "dumb" calculator - takes RPlusDiscount from cache, only calculates partner part.
/// Formula: PartnerDiscount = MaxDiscount * (CurrentLevel / TotalLevels)
/// </summary>
public sealed class DynamicLevelDiscountCalculator : IDiscountCalculator
{
    // Category Defaults for MaxDiscount
    private static readonly Dictionary<string, decimal> CategoryDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["restaurant"] = 15m,
        ["services"] = 10m,
        ["retail"] = 5m,
    };
    private const decimal DefaultMaxDiscount = 3m;

    public DiscountResult Calculate(CachedUserProfile user, PartnerDiscountConfig partner)
    {
        // --- PROTECTION: Divide by zero ---
        int totalLevels = user.TotalLevels > 0 ? user.TotalLevels : 1;
        int currentLevel = Math.Clamp(user.CurrentLevel, 1, totalLevels);
        bool isHappyHour = false;

        // --- STEP 1: Check Happy Hours (Level Boost) ---
        var happyConfig = HappyHoursConfig.FromJson(partner.HappyHoursConfigJson);
        if (happyConfig is { Enabled: true })
        {
            var utcNow = DateTimeOffset.UtcNow;
            foreach (var schedule in happyConfig.ScheduleUtc)
            {
                if (schedule.IsActiveNow(utcNow))
                {
                    currentLevel += happyConfig.LevelBoost;
                    isHappyHour = true;
                    break;
                }
            }
        }

        // Hard Cap: Level cannot exceed TotalLevels
        if (currentLevel > totalLevels)
            currentLevel = totalLevels;

        // --- STEP 2: Partner Discount ---
        decimal partnerDiscount;

        if (partner.DiscountStrategy.Equals("fixed", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy fixed discount - no ratio calculation
            partnerDiscount = partner.FixedDiscount ?? 0m;
        }
        else
        {
            // v3.0: Dynamic level-based discount with ratio
            // Formula: PartnerDiscount = MaxDiscount * (CurrentLevel / TotalLevels)
            decimal maxDiscount = partner.MaxDiscount ?? GetDefaultByCategory(partner.PartnerCategory);
            decimal ratio = (decimal)currentLevel / totalLevels;
            partnerDiscount = maxDiscount * ratio;
        }

        // --- STEP 3: Total Discount ---
        // RPlus discount comes ready from Loyalty (cache), we just add partner part
        decimal total = user.RPlusDiscount + partnerDiscount;

        return new DiscountResult(
            Math.Round(user.RPlusDiscount, 2),
            Math.Round(partnerDiscount, 2),
            Math.Round(total, 2),
            currentLevel,
            isHappyHour);
    }

    private static decimal GetDefaultByCategory(string category)
    {
        return CategoryDefaults.TryGetValue(category, out var max) ? max : DefaultMaxDiscount;
    }
}
