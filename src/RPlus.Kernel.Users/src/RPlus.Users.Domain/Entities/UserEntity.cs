using System;
using RPlus.SDK.Users.Models;
using RPlus.SDK.Users.Enums;

#nullable enable
namespace RPlus.Users.Domain.Entities;

public sealed class UserEntity : User
{
    private UserEntity()
    {
    }

    public static UserEntity Create(
        Guid id,
        string? preferredName,
        string locale,
        string timeZone,
        DateTime now)
    {
        return new UserEntity
        {
            Id = id,
            PreferredName = preferredName,
            Locale = locale ?? "en-US",
            TimeZone = timeZone ?? "UTC",
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateStatus(UserStatus newStatus, DateTime now)
    {
        if (this.Status == newStatus)
            return;
        this.Status = newStatus;
        this.UpdatedAt = now;
    }

    public void UpdateProfile(
        string? preferredName,
        string? locale,
        string? timeZone,
        string? avatarId,
        DateTime now)
    {
        this.PreferredName = preferredName;
        if (!string.IsNullOrWhiteSpace(locale))
            this.Locale = locale;
        if (!string.IsNullOrWhiteSpace(timeZone))
            this.TimeZone = timeZone;
        this.AvatarId = avatarId;
        this.UpdatedAt = now;
    }
}
