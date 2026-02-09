using System;

namespace RPlus.Loyalty.Application.Abstractions;

public sealed record UserContext(
    Guid UserId,
    DateTime CreatedAtUtc,
    string Status,
    bool IsVip,
    int TenureDays,
    int TenureYears,
    bool IsBirthdayToday,
    bool HasDisability,
    int ChildrenCount,
    bool IsBoss,
    string Level,
    string[] Tags,
    string FirstName,
    string LastName,
    string PreferredName);
