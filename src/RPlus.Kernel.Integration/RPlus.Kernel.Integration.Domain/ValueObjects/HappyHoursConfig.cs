using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPlus.Kernel.Integration.Domain.ValueObjects;

/// <summary>
/// Happy Hours configuration for partner discount boost.
/// </summary>
public sealed class HappyHoursConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
    
    [JsonPropertyName("level_boost")]
    public int LevelBoost { get; init; } = 1;
    
    [JsonPropertyName("schedule_utc")]
    public List<HappyHourSchedule> ScheduleUtc { get; init; } = new();

    public static HappyHoursConfig? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<HappyHoursConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}

public sealed class HappyHourSchedule
{
    [JsonPropertyName("day_of_week")]
    public int DayOfWeek { get; init; } // 0=Sunday, 1=Monday, etc.
    
    [JsonPropertyName("start")]
    public string Start { get; init; } = "00:00";
    
    [JsonPropertyName("end")]
    public string End { get; init; } = "23:59";

    public bool IsActiveNow(DateTimeOffset utcNow)
    {
        if ((int)utcNow.DayOfWeek != DayOfWeek)
            return false;

        if (!TimeOnly.TryParse(Start, out var startTime) || !TimeOnly.TryParse(End, out var endTime))
            return false;

        var currentTime = TimeOnly.FromDateTime(utcNow.UtcDateTime);
        return currentTime >= startTime && currentTime <= endTime;
    }
}
