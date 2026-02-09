using System;
using System.Collections.Generic;

namespace RPlus.SDK.Gateway.Models;

public class AppRelease
{
    public Guid Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MinVersionCode { get; set; }
    public int LatestVersionCode { get; set; }
    public Dictionary<string, string> StoreUrls { get; set; } = new Dictionary<string, string>();
    public string? Message { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
