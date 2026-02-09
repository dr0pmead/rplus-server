using System;

using RPlus.SDK.Users.Enums;

#nullable enable
namespace RPlus.SDK.Users.Models;

public class User
{
    public Guid Id { get; set; }
    
    // FIO moved to HR.EmployeeProfile
    // FirstName, LastName, MiddleName removed
    
    public string? PreferredName { get; set; }
    public string Locale { get; set; } = "en-US";
    public string TimeZone { get; set; } = "UTC";
    public string? AvatarId { get; set; }
    public string PreferencesJson { get; set; } = "{}";
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
