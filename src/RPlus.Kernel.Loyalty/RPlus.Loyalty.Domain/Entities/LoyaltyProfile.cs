using System;
using RPlus.SDK.Loyalty.Models;

namespace RPlus.Loyalty.Domain.Entities;

public class LoyaltyProfile : RPlus.SDK.Loyalty.Models.LoyaltyProfile
{
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    private LoyaltyProfile()
    {
    }

    public static LoyaltyProfile Create(Guid userId, Guid? levelId = null)
    {
        return new LoyaltyProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LevelId = levelId ?? Guid.Empty,
            PointsBalance = 0,
            LastActivityAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 0
        };
    }

    public void ApplyPoints(decimal delta)
    {
        PointsBalance += delta;
        LastActivityAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void SetLevel(Guid? levelId)
    {
        LevelId = levelId ?? Guid.Empty;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}
