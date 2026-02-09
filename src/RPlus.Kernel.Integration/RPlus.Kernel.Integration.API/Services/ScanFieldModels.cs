using System.Collections.Generic;

namespace RPlus.Kernel.Integration.Api.Services;

public sealed record ScanFieldDefinition(
    string Key,
    string Title,
    string Group,
    string Type,
    string? Description,
    string Resolver,
    IReadOnlyDictionary<string, string> ResolverConfig,
    IReadOnlyCollection<string> Requires,
    int? SortOrder,
    bool IsAdvanced,
    bool Expose);

public sealed record ScanFieldCatalog(IReadOnlyDictionary<string, ScanFieldDefinition> Fields);

public sealed record ScanFieldDto(
    string Key,
    string Title,
    string Group,
    string Type,
    string? Description,
    int? SortOrder,
    bool IsAdvanced,
    bool Expose);
