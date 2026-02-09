using System.Collections.Generic;

namespace RPlus.Kernel.Integration.Api.Models;

public sealed class ScanFieldAdminUpsertRequest
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? Group { get; set; }
    public string? Type { get; set; }
    public string? Resolver { get; set; }
    public Dictionary<string, string>? ResolverConfig { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsAdvanced { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? Expose { get; set; }
}

public sealed class ScanFieldAdminItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Resolver { get; set; } = string.Empty;
    public Dictionary<string, string>? ResolverConfig { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public bool IsAdvanced { get; set; }
    public bool IsEnabled { get; set; }
    public bool Expose { get; set; }
    public bool IsCustom { get; set; }
}

public sealed class ScanFieldSourceInfo
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class ScanFieldSourceCatalog
{
    public IReadOnlyList<string> Groups { get; set; } = new List<string>();
    public IReadOnlyList<string> Types { get; set; } = new List<string>();
    public IReadOnlyList<string> Resolvers { get; set; } = new List<string>();
    public IReadOnlyList<ScanFieldSourceInfo> UserProfileFields { get; set; } = new List<ScanFieldSourceInfo>();
    public IReadOnlyList<ScanFieldSourceInfo> LoyaltyProfileFields { get; set; } = new List<ScanFieldSourceInfo>();
    public IReadOnlyList<ScanFieldSourceInfo> MetaFields { get; set; } = new List<ScanFieldSourceInfo>();
    public IReadOnlyList<ScanFieldSourceInfo> UserMetaFields { get; set; } = new List<ScanFieldSourceInfo>();
    public IReadOnlyList<ScanFieldSourceInfo> PartnerMetaFields { get; set; } = new List<ScanFieldSourceInfo>();
    public IReadOnlyList<ScanFieldSourceInfo> ExistingKeys { get; set; } = new List<ScanFieldSourceInfo>();
}

