using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Api.Models;
using RPlus.Kernel.Integration.Application;
using RPlusGrpc.Meta;
using System.Text.Json;
using System.Linq;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IScanFieldCatalogService
{
    Task<ScanFieldCatalog> GetCatalogAsync(CancellationToken cancellationToken);
    Task<ScanFieldCatalog> GetBaseCatalogAsync(CancellationToken cancellationToken);
    Task<ScanFieldSourceCatalog> GetSourceCatalogAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> ValidateFieldsAsync(IReadOnlyCollection<string> fields, CancellationToken cancellationToken);
}

public sealed class ScanFieldCatalogService : IScanFieldCatalogService
{
    private const string CacheKey = "scan_field_catalog_v1";
    private const string BaseCacheKey = "scan_field_catalog_base_v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScanFieldCatalogService> _logger;
    private readonly IOptionsMonitor<IntegrationMetaOptions> _metaOptions;
    public ScanFieldCatalogService(
        MetaService.MetaServiceClient metaClient,
        IMemoryCache cache,
        ILogger<ScanFieldCatalogService> logger,
        IOptionsMonitor<IntegrationMetaOptions> metaOptions)
    {
        _metaClient = metaClient;
        _cache = cache;
        _logger = logger;
        _metaOptions = metaOptions;
    }

    public async Task<ScanFieldCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out ScanFieldCatalog? cached) && cached != null)
            return cached;

        var catalog = await BuildCatalogAsync(includeOverlay: true, cancellationToken);
        _cache.Set(CacheKey, catalog, CacheTtl);
        return catalog;
    }

    public async Task<ScanFieldCatalog> GetBaseCatalogAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(BaseCacheKey, out ScanFieldCatalog? cached) && cached != null)
            return cached;

        var catalog = await BuildCatalogAsync(includeOverlay: false, cancellationToken);
        _cache.Set(BaseCacheKey, catalog, CacheTtl);
        return catalog;
    }

    public Task<ScanFieldSourceCatalog> GetSourceCatalogAsync(CancellationToken cancellationToken)
    {
        var userProfile = BuildUserProfileSources().ToList();
        var loyaltyProfile = BuildLoyaltyProfileSources().ToList();
        var partnerProfile = BuildPartnerSources().ToList();

        var existingKeys = userProfile
            .Concat(loyaltyProfile)
            .Concat(partnerProfile)
            .OrderBy(x => x.Label)
            .ToList();

        return Task.FromResult(new ScanFieldSourceCatalog
        {
            Groups = new[] { "user", "loyalty", "partner" },
            Types = new[] { "string", "number" },
            Resolvers = new[] { "profile", "loyalty_profile", "partner" },
            UserProfileFields = userProfile,
            LoyaltyProfileFields = loyaltyProfile,
            PartnerMetaFields = partnerProfile,
            MetaFields = Array.Empty<ScanFieldSourceInfo>(),
            UserMetaFields = Array.Empty<ScanFieldSourceInfo>(),
            ExistingKeys = existingKeys
        });
    }

    public async Task<IReadOnlyCollection<string>> ValidateFieldsAsync(IReadOnlyCollection<string> fields, CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
            return Array.Empty<string>();

        var catalog = await GetCatalogAsync(cancellationToken);
        var missing = new List<string>();
        foreach (var field in fields)
        {
            var key = NormalizeKey(field);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!catalog.Fields.ContainsKey(key))
                missing.Add(field);
        }

        return missing;
    }

    private async Task<ScanFieldCatalog> BuildCatalogAsync(bool includeOverlay, CancellationToken cancellationToken)
    {
        var items = new Dictionary<string, ScanFieldDefinition>(StringComparer.OrdinalIgnoreCase);
        BuildDefaultFields(items);

        if (includeOverlay)
        {
            await ApplyOverlayFromMetaAsync(items, cancellationToken);
        }

        return new ScanFieldCatalog(items);
    }

    private static void BuildDefaultFields(Dictionary<string, ScanFieldDefinition> items)
    {
        items["user.firstname"] = new ScanFieldDefinition(
            Key: "user.firstName",
            Title: "First name",
            Group: "user",
            Type: "string",
            Description: null,
            Resolver: "profile",
            ResolverConfig: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "firstName" },
            Requires: new[] { "usersProfile" },
            SortOrder: 10,
            IsAdvanced: false,
            Expose: true);

        items["user.lastname"] = new ScanFieldDefinition(
            Key: "user.lastName",
            Title: "Last name",
            Group: "user",
            Type: "string",
            Description: null,
            Resolver: "profile",
            ResolverConfig: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "lastName" },
            Requires: new[] { "usersProfile" },
            SortOrder: 20,
            IsAdvanced: false,
            Expose: true);

        items["user.avatarurl"] = new ScanFieldDefinition(
            Key: "user.avatarUrl",
            Title: "Avatar URL",
            Group: "user",
            Type: "string",
            Description: null,
            Resolver: "profile",
            ResolverConfig: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "avatarUrl" },
            Requires: new[] { "usersProfile" },
            SortOrder: 30,
            IsAdvanced: false,
            Expose: true);

        items["discountuser"] = new ScanFieldDefinition(
            Key: "discountUser",
            Title: "User discount",
            Group: "loyalty",
            Type: "number",
            Description: null,
            Resolver: "loyalty_profile",
            ResolverConfig: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "totalDiscount" },
            Requires: new[] { "loyaltyProfile" },
            SortOrder: 100,
            IsAdvanced: false,
            Expose: true);

        items["discountpartner"] = new ScanFieldDefinition(
            Key: "discountPartner",
            Title: "Partner discount",
            Group: "partner",
            Type: "number",
            Description: null,
            Resolver: "partner",
            ResolverConfig: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "discountPartner" },
            Requires: new[] { "partnerProfile" },
            SortOrder: 110,
            IsAdvanced: false,
            Expose: true);
    }

    private async Task ApplyOverlayFromMetaAsync(
        Dictionary<string, ScanFieldDefinition> items,
        CancellationToken cancellationToken)
    {
        try
        {
            var listResponse = await _metaClient.GetListByKeyAsync(
                new GetListByKeyRequest { Key = "scan_fields" },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            if (!listResponse.Found)
                return;

            var listItems = await _metaClient.GetListItemsAsync(
                new GetListItemsRequest { ListId = listResponse.List.Id },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            foreach (var item in listItems.Items)
            {
                var key = NormalizeKey(item.Code);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!items.TryGetValue(key, out var existing))
                    continue;

                if (!TryReadOverlay(item, out var overlay))
                    continue;

                if (overlay.IsEnabled.HasValue && overlay.IsEnabled.Value is false)
                {
                    items.Remove(key);
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(overlay.Title) ? existing.Title : overlay.Title!;
                var group = string.IsNullOrWhiteSpace(overlay.Group) ? existing.Group : NormalizeGroup(overlay.Group);
                var description = overlay.Description ?? existing.Description;
                var sortOrder = overlay.SortOrder ?? existing.SortOrder;
                var isAdvanced = overlay.IsAdvanced ?? existing.IsAdvanced;
                var expose = overlay.Expose ?? existing.Expose;

                items[key] = existing with
                {
                    Title = title,
                    Group = string.IsNullOrWhiteSpace(group) ? existing.Group : group,
                    Description = description,
                    SortOrder = sortOrder,
                    IsAdvanced = isAdvanced,
                    Expose = expose
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply scan_fields overlay from Meta");
        }
    }

    private async Task<(List<ScanFieldSourceInfo> CustomKeys, List<ScanFieldMetaItem> Items)> LoadScanFieldCustomDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var listResponse = await _metaClient.GetListByKeyAsync(
                new GetListByKeyRequest { Key = "scan_fields" },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            if (!listResponse.Found)
                return (new List<ScanFieldSourceInfo>(), new List<ScanFieldMetaItem>());

            var listItems = await _metaClient.GetListItemsAsync(
                new GetListItemsRequest { ListId = listResponse.List.Id },
                BuildMetadata(),
                cancellationToken: cancellationToken);

            var customKeys = new List<ScanFieldSourceInfo>();
            var items = new List<ScanFieldMetaItem>();

            foreach (var item in listItems.Items)
            {
                var parsed = ReadMetaItem(item);
                if (parsed == null)
                    continue;

                items.Add(parsed);

                if (parsed.IsCustom && !string.IsNullOrWhiteSpace(parsed.Key))
                {
                    customKeys.Add(new ScanFieldSourceInfo
                    {
                        Key = parsed.Key!,
                        Label = parsed.Title ?? parsed.Key!,
                        Type = NormalizeType(parsed.Type)
                    });
                }
            }

            return (customKeys, items);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load scan_fields custom definitions from Meta");
            return (new List<ScanFieldSourceInfo>(), new List<ScanFieldMetaItem>());
        }
    }

    private static IEnumerable<ScanFieldSourceInfo> BuildUserProfileSources()
    {
        return new[]
        {
            new ScanFieldSourceInfo { Key = "firstName", Label = "First name", Type = "string" },
            new ScanFieldSourceInfo { Key = "lastName", Label = "Last name", Type = "string" },
            new ScanFieldSourceInfo { Key = "avatarUrl", Label = "Avatar URL", Type = "string" }
        };
    }

    private static IEnumerable<ScanFieldSourceInfo> BuildLoyaltyProfileSources()
    {
        return new[]
        {
            new ScanFieldSourceInfo { Key = "totalDiscount", Label = "Total discount", Type = "number" }
        };
    }

    private static IEnumerable<ScanFieldSourceInfo> BuildPartnerSources()
    {
        return new[]
        {
            new ScanFieldSourceInfo { Key = "discountPartner", Label = "Partner discount", Type = "number" }
        };
    }

    private static string NormalizeKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private Metadata? BuildMetadata()
    {
        var secret = _metaOptions.CurrentValue.ServiceSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            return new Metadata { { "x-rplus-service-secret", secret.Trim() } };

        return null;
    }

    private static string NormalizeType(string? value)
    {
        var normalized = (value ?? "string").Trim().ToLowerInvariant();
        return normalized switch
        {
            "number" => "number",
            "boolean" => "boolean",
            "datetime" => "datetime",
            "json" => "json",
            _ => "string"
        };
    }

    private static string NormalizeGroup(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "user" => "user",
            "loyalty" => "loyalty",
            "partner" => "partner",
            _ => string.Empty
        };
    }

    private static bool IsAllowedResolver(string? resolver)
    {
        if (string.IsNullOrWhiteSpace(resolver))
            return false;

        return resolver.Trim().ToLowerInvariant() switch
        {
            "profile" => true,
            "loyalty_profile" => true,
            "partner" => true,
            _ => false
        };
    }

    private static IReadOnlyCollection<string> BuildRequires(string resolver, IReadOnlyDictionary<string, string> config)
    {
        var normalized = resolver.Trim().ToLowerInvariant();
        return normalized switch
        {
            "profile" => new[] { "usersProfile" },
            "loyalty_profile" => new[] { "loyaltyProfile" },
            "partner" => new[] { "partnerMeta" },
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyCollection<string> BuildSumRequires(IReadOnlyDictionary<string, string> config)
    {
        var requires = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in ParseFieldList(config))
        {
            var normalized = NormalizeKey(field);
            if (normalized.StartsWith("user.meta."))
                requires.Add("usersMeta");
            else if (normalized.StartsWith("user."))
                requires.Add("usersProfile");
            else if (normalized.StartsWith("loyalty."))
                requires.Add("loyaltyProfile");
            else if (normalized.StartsWith("partner.meta."))
                requires.Add("partnerMeta");
            else if (normalized.StartsWith("partner."))
                requires.Add("partnerMeta");
            else if (normalized.StartsWith("meta.") && !string.Equals(normalized, "meta.servertime", StringComparison.OrdinalIgnoreCase))
                requires.Add("partnerMeta");
        }
        return requires;
    }

    private static IReadOnlyCollection<string> ParseFieldList(IReadOnlyDictionary<string, string> config)
    {
        if (!config.TryGetValue("fields", out var raw) || string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        raw = raw.Trim();
        if (raw.StartsWith("["))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var value = item.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                list.Add(value);
                        }
                    }
                    return list;
                }
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        return raw
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();
    }
    private static IReadOnlyCollection<string> BuildListLookupRequires(IReadOnlyDictionary<string, string> config)
    {
        var requires = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "metaList" };
        if (config.TryGetValue("matchField", out var matchField) && !string.IsNullOrWhiteSpace(matchField))
        {
            var normalized = NormalizeKey(matchField);
            if (normalized.StartsWith("user.meta."))
                requires.Add("usersMeta");
            else if (normalized.StartsWith("user."))
                requires.Add("usersProfile");
            else if (normalized.StartsWith("loyalty."))
                requires.Add("loyaltyProfile");
            else if (normalized.StartsWith("partner.meta."))
                requires.Add("partnerMeta");
            else if (normalized.StartsWith("partner."))
                requires.Add("partnerMeta");
            else if (normalized.StartsWith("meta.") && !string.Equals(normalized, "meta.servertime", StringComparison.OrdinalIgnoreCase))
                requires.Add("partnerMeta");
        }
        return requires;
    }

    private static bool TryReadOverlay(MetaListItem item, out ScanFieldOverlay overlay)
    {
        overlay = new ScanFieldOverlay();

        if (string.IsNullOrWhiteSpace(item.ValueJson))
        {
            overlay = overlay with { Title = item.Title };
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(item.ValueJson);
            var root = doc.RootElement;

            overlay = overlay with
            {
                Title = ReadString(root, "title") ?? item.Title,
                Group = ReadString(root, "group"),
                Description = ReadString(root, "description"),
                SortOrder = ReadInt(root, "sortOrder"),
                IsAdvanced = ReadBool(root, "isAdvanced"),
                IsEnabled = ReadBool(root, "isEnabled"),
                Expose = ReadBool(root, "expose")
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
            return result;

        return null;
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private sealed record ScanFieldOverlay(
        string? Title = null,
        string? Group = null,
        string? Description = null,
        int? SortOrder = null,
        bool? IsAdvanced = null,
        bool? IsEnabled = null,
        bool? Expose = null);

    private sealed class ScanFieldMetaItem
    {
        public string? Key { get; init; }
        public string? Title { get; init; }
        public string? Group { get; init; }
        public string? Type { get; init; }
        public string? Resolver { get; init; }
        public Dictionary<string, string> ResolverConfig { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string? Description { get; init; }
        public int? SortOrder { get; init; }
        public bool? IsAdvanced { get; init; }
        public bool? IsEnabled { get; init; }
        public bool? Expose { get; init; }
        public bool IsCustom { get; init; }
    }

    private static bool TryReadCustomField(MetaListItem item, out ScanFieldMetaItem meta)
    {
        meta = new ScanFieldMetaItem();
        var parsed = ReadMetaItem(item);
        if (parsed == null || !parsed.IsCustom)
            return false;
        meta = parsed;
        return true;
    }

    private static ScanFieldMetaItem? ReadMetaItem(MetaListItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ValueJson))
            return new ScanFieldMetaItem { Key = NormalizeKey(item.Code), Title = item.Title, IsCustom = false };

        try
        {
            using var doc = JsonDocument.Parse(item.ValueJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var root = doc.RootElement;
            var resolver = ReadString(root, "resolver");
            var resolverConfig = ReadDictionary(root, "resolverConfig");

            return new ScanFieldMetaItem
            {
                Key = NormalizeKey(item.Code),
                Title = ReadString(root, "title") ?? item.Title,
                Group = ReadString(root, "group"),
                Type = ReadString(root, "type"),
                Resolver = resolver,
                ResolverConfig = resolverConfig,
                Description = ReadString(root, "description"),
                SortOrder = ReadInt(root, "sortOrder"),
                IsAdvanced = ReadBool(root, "isAdvanced"),
                IsEnabled = ReadBool(root, "isEnabled"),
                Expose = ReadBool(root, "expose"),
                IsCustom = !string.IsNullOrWhiteSpace(resolver)
            };
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ReadDictionary(JsonElement root, string name)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in value.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                result[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }
            else
            {
                result[prop.Name] = prop.Value.GetRawText();
            }
        }

        return result;
    }

    private static string GetConfigValue(ScanFieldDefinition definition, string key)
    {
        if (definition.ResolverConfig.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        return string.Empty;
    }

    private static string MapMetaType(string? dataType)
    {
        return (dataType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "number" => "number",
            "boolean" => "boolean",
            "datetime" => "datetime",
            "json" => "json",
            _ => "string"
        };
    }
}
