using System.Text.Json;
using System.Linq;

namespace RPlus.Kernel.Integration.Api.Services;

public sealed record ScanFieldResolutionPlan(
    IReadOnlyList<ScanFieldDefinition> Fields,
    IReadOnlyCollection<string> MissingFields,
    IReadOnlyCollection<string> MetaKeys,
    IReadOnlyCollection<string> PartnerMetaKeys,
    bool RequiresUsersProfile,
    bool RequiresUsersMeta,
    bool RequiresPartnerMeta,
    bool RequiresLoyalty,
    bool RequiresMetaList);

public interface IScanFieldResolver
{
    Task<ScanFieldResolutionPlan> BuildPlanAsync(IReadOnlyCollection<string> fields, CancellationToken cancellationToken);
    Task<object> BuildPayloadAsync(
        ScanFieldResolutionPlan plan,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        CancellationToken cancellationToken);
}

public sealed class ScanFieldResolver : IScanFieldResolver
{
    private readonly IScanFieldCatalogService _catalogService;
    private readonly IMetaListLookupService _listLookup;

    // Resolution order (documented):
    // 1) field.source -> rawValue
    // 2) if key starts with meta.* -> integration_partner meta (except meta.serverTime)
    // 3) list lookup (if configured)
    // 4) computed rules (future)
    // 5) output only when Expose = true
    public ScanFieldResolver(
        IScanFieldCatalogService catalogService,
        IMetaListLookupService listLookup)
    {
        _catalogService = catalogService;
        _listLookup = listLookup;
    }

    public async Task<ScanFieldResolutionPlan> BuildPlanAsync(
        IReadOnlyCollection<string> fields,
        CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetCatalogAsync(cancellationToken);
        var requested = new List<ScanFieldDefinition>();
        var missing = new List<string>();
        var metaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var partnerMetaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiresUsers = false;
        var requiresUsersMeta = false;
        var requiresPartnerMeta = false;
        var requiresLoyalty = false;
        var requiresList = false;

        foreach (var raw in fields)
        {
            var key = NormalizeKey(raw);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!catalog.Fields.TryGetValue(key, out var def))
            {
                missing.Add(raw);
                continue;
            }

            requested.Add(def);

            if (def.Requires?.Contains("usersProfile", StringComparer.OrdinalIgnoreCase) == true)
                requiresUsers = true;
            if (def.Requires?.Contains("usersMeta", StringComparer.OrdinalIgnoreCase) == true)
                requiresUsersMeta = true;
            if (def.Requires?.Contains("loyaltyProfile", StringComparer.OrdinalIgnoreCase) == true)
                requiresLoyalty = true;
            if (def.Requires?.Contains("metaList", StringComparer.OrdinalIgnoreCase) == true)
                requiresList = true;

            if (string.Equals(def.Resolver, "user_meta", StringComparison.OrdinalIgnoreCase))
            {
                if (def.ResolverConfig.TryGetValue("key", out var metaKey) && !string.IsNullOrWhiteSpace(metaKey))
                    metaKeys.Add(metaKey.Trim());
                requiresUsersMeta = true;
            }

            if (string.Equals(def.Resolver, "partner_meta", StringComparison.OrdinalIgnoreCase))
            {
                if (def.ResolverConfig.TryGetValue("key", out var metaKey) && !string.IsNullOrWhiteSpace(metaKey))
                    partnerMetaKeys.Add(metaKey.Trim());
                requiresPartnerMeta = true;
            }
        }

        return new ScanFieldResolutionPlan(
            Fields: requested,
            MissingFields: missing,
            MetaKeys: metaKeys,
            PartnerMetaKeys: partnerMetaKeys,
            RequiresUsersProfile: requiresUsers || requiresUsersMeta,
            RequiresUsersMeta: requiresUsersMeta,
            RequiresPartnerMeta: requiresPartnerMeta,
            RequiresLoyalty: requiresLoyalty,
            RequiresMetaList: requiresList);
    }

    public async Task<object> BuildPayloadAsync(
        ScanFieldResolutionPlan plan,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        CancellationToken cancellationToken)
    {
        var catalog = await _catalogService.GetCatalogAsync(cancellationToken);
        var userGroup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var loyaltyGroup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var metaGroup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?>? userMetaGroup = null;
        Dictionary<string, object?>? partnerGroup = null;
        Dictionary<string, object?>? partnerMetaGroup = null;

        var listCache = new Dictionary<string, IReadOnlyList<MetaListLookupItem>>(StringComparer.OrdinalIgnoreCase);
        var valueCache = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in plan.Fields)
        {
            var value = await ResolveValueAsync(def, user, loyalty, partnerMeta, serverTime, listCache, valueCache, catalog.Fields, 0, cancellationToken);
            if (!def.Expose)
                continue;
            if (def.Group.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                if (def.Key.StartsWith("user.meta.", StringComparison.OrdinalIgnoreCase))
                {
                    userMetaGroup ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    var leaf = def.Key.Substring("user.meta.".Length);
                    userMetaGroup[leaf] = value;
                }
                else
                {
                    var leaf = GetLeaf(def.Key);
                    userGroup[leaf] = value;
                }
            }
            else if (def.Group.Equals("loyalty", StringComparison.OrdinalIgnoreCase))
            {
                var leaf = GetLeaf(def.Key);
                loyaltyGroup[leaf] = value;
            }
            else if (def.Group.Equals("partner", StringComparison.OrdinalIgnoreCase))
            {
                if (def.Key.StartsWith("partner.meta.", StringComparison.OrdinalIgnoreCase))
                {
                    partnerMetaGroup ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    var leaf = def.Key.Substring("partner.meta.".Length);
                    partnerMetaGroup[leaf] = value;
                }
                else
                {
                    partnerGroup ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    var leaf = GetLeaf(def.Key);
                    partnerGroup[leaf] = value;
                }
            }
            else if (def.Group.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                var leaf = GetLeaf(def.Key);
                metaGroup[leaf] = value;
            }
        }

        if (userMetaGroup is not null && userMetaGroup.Count > 0)
        {
            userGroup["meta"] = userMetaGroup;
        }

        if (partnerMetaGroup is not null && partnerMetaGroup.Count > 0)
        {
            partnerGroup ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            partnerGroup["meta"] = partnerMetaGroup;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (userGroup.Count > 0)
            result["user"] = userGroup;
        if (loyaltyGroup.Count > 0)
            result["loyalty"] = loyaltyGroup;
        if (partnerGroup is not null && partnerGroup.Count > 0)
            result["partner"] = partnerGroup;
        if (metaGroup.Count > 0)
            result["meta"] = metaGroup;

        return result;
    }

    private async Task<object?> ResolveValueAsync(
        ScanFieldDefinition def,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        Dictionary<string, IReadOnlyList<MetaListLookupItem>> listCache,
        Dictionary<string, object?> valueCache,
        IReadOnlyDictionary<string, ScanFieldDefinition> catalog,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 3)
            return null;

        if (valueCache.TryGetValue(def.Key, out var cached))
            return cached;

        object? value = def.Resolver.ToLowerInvariant() switch
        {
            "profile" => ResolveProfileValue(def, user),
            "loyalty_profile" => ResolveLoyaltyValue(def, loyalty),
            "partner" => ResolvePartnerValue(def, partnerMeta),
            "meta" => ResolveMetaValue(def, serverTime),
            "user_meta" => ResolveUserMetaValue(def, user),
            "partner_meta" => ResolvePartnerMetaValue(def, partnerMeta),
            "list_lookup" => await ResolveListLookupAsync(def, user, loyalty, partnerMeta, serverTime, listCache, valueCache, cancellationToken),
            "sum" => await ResolveSumAsync(def, user, loyalty, partnerMeta, serverTime, listCache, valueCache, catalog, depth + 1, cancellationToken),
            _ => null
        };

        valueCache[def.Key] = value;
        return value;
    }

    private async Task<object?> ResolveSumAsync(
        ScanFieldDefinition def,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        Dictionary<string, IReadOnlyList<MetaListLookupItem>> listCache,
        Dictionary<string, object?> valueCache,
        IReadOnlyDictionary<string, ScanFieldDefinition> catalog,
        int depth,
        CancellationToken cancellationToken)
    {
        var fields = ParseFieldList(def);
        if (fields.Count == 0)
            return null;

        var sum = 0d;
        var hasValue = false;
        var self = NormalizeKey(def.Key);

        foreach (var fieldKey in fields)
        {
            var normalized = NormalizeKey(fieldKey);
            if (string.IsNullOrWhiteSpace(normalized) || string.Equals(normalized, self, StringComparison.OrdinalIgnoreCase))
                continue;

            object? value;
            if (catalog.TryGetValue(normalized, out var nested))
            {
                value = await ResolveValueAsync(nested, user, loyalty, partnerMeta, serverTime, listCache, valueCache, catalog, depth, cancellationToken);
            }
            else
            {
                value = await ResolveMatchValueAsync(fieldKey, user, loyalty, partnerMeta, serverTime, valueCache, cancellationToken);
            }

            if (TryToNumber(value, out var number))
            {
                sum += number;
                hasValue = true;
            }
        }

        return hasValue ? sum : null;
    }

    private object? ResolveProfileValue(ScanFieldDefinition def, UsersProfileDto? user)
    {
        if (user is null)
            return null;

        var path = GetConfigValue(def, "path");
        return path.ToLowerInvariant() switch
        {
            "id" => user.Id.ToString(),
            "firstname" => user.FirstName,
            "lastname" => user.LastName,
            "middlename" => user.MiddleName,
            "preferredname" => user.PreferredName,
            "locale" => user.Locale,
            "timezone" => user.TimeZone,
            "status" => user.Status,
            "createdat" => user.CreatedAt.ToString("O"),
            "updatedat" => user.UpdatedAt.ToString("O"),
            "avatarid" => user.AvatarId,
            "avatarurl" => user.AvatarId,
            _ => null
        };
    }

    private object? ResolveLoyaltyValue(ScanFieldDefinition def, LoyaltyProfileDto? loyalty)
    {
        if (loyalty is null)
            return null;

        var path = GetConfigValue(def, "path");
        return path.ToLowerInvariant() switch
        {
            "level" => loyalty.Level,
            "discount" => loyalty.Discount,
            "motivationdiscount" => loyalty.MotivationDiscount,
            "totaldiscount" => loyalty.TotalDiscount,
            "points" => loyalty.PointsBalance,
            "canburn" => loyalty.PointsBalance > 0,
            _ => null
        };
    }

    private object? ResolveMetaValue(ScanFieldDefinition def, DateTimeOffset serverTime)
    {
        var path = GetConfigValue(def, "path");
        return path.ToLowerInvariant() switch
        {
            "servertime" => serverTime.ToString("O"),
            _ => null
        };
    }

    private object? ResolveUserMetaValue(ScanFieldDefinition def, UsersProfileDto? user)
    {
        if (user is null)
            return null;

        if (!def.ResolverConfig.TryGetValue("key", out var metaKey) || string.IsNullOrWhiteSpace(metaKey))
            return null;

        if (!user.MetaJson.TryGetValue(metaKey, out var metaValue))
            return null;

        return metaValue;
    }

    private object? ResolvePartnerMetaValue(ScanFieldDefinition def, IReadOnlyDictionary<string, object?>? partnerMeta)
    {
        if (partnerMeta is null)
            return null;

        if (!def.ResolverConfig.TryGetValue("key", out var metaKey) || string.IsNullOrWhiteSpace(metaKey))
            return null;

        return partnerMeta.TryGetValue(metaKey, out var metaValue) ? metaValue : null;
    }

    private object? ResolvePartnerValue(ScanFieldDefinition def, IReadOnlyDictionary<string, object?>? partnerMeta)
    {
        if (partnerMeta is null)
            return null;

        var path = GetConfigValue(def, "path");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return partnerMeta.TryGetValue(path, out var metaValue) ? metaValue : null;
    }

    private async Task<object?> ResolveListLookupAsync(
        ScanFieldDefinition def,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        Dictionary<string, IReadOnlyList<MetaListLookupItem>> listCache,
        Dictionary<string, object?> valueCache,
        CancellationToken cancellationToken)
    {
        var listKey = GetConfigValue(def, "listKey");
        var matchField = GetConfigValue(def, "matchField");
        var valueField = GetConfigValue(def, "valueField");
        if (string.IsNullOrWhiteSpace(listKey) || string.IsNullOrWhiteSpace(matchField) || string.IsNullOrWhiteSpace(valueField))
            return null;

        if (string.Equals(NormalizeKey(matchField), NormalizeKey(def.Key), StringComparison.OrdinalIgnoreCase))
            return null;

        var matchValue = await ResolveMatchValueAsync(matchField, user, loyalty, partnerMeta, serverTime, valueCache, cancellationToken);
        if (matchValue is null)
            return null;

        if (!listCache.TryGetValue(listKey, out var list))
        {
            list = await _listLookup.GetListAsync(listKey, cancellationToken);
            listCache[listKey] = list;
        }

        if (list.Count == 0)
            return null;

        var matchText = matchValue.ToString();
        if (string.IsNullOrWhiteSpace(matchText))
            return null;

        var item = list.FirstOrDefault(x =>
            string.Equals(x.ExternalId, matchText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Code, matchText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Title, matchText, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return null;

        return valueField.ToLowerInvariant() switch
        {
            "code" => item.Code,
            "title" => item.Title,
            "externalid" => item.ExternalId,
            _ => ReadValue(item.Value, valueField)
        };
    }

    private Task<object?> ResolveMatchValueAsync(
        string matchField,
        UsersProfileDto? user,
        LoyaltyProfileDto? loyalty,
        IReadOnlyDictionary<string, object?>? partnerMeta,
        DateTimeOffset serverTime,
        Dictionary<string, object?> valueCache,
        CancellationToken cancellationToken)
    {
        if (valueCache.TryGetValue(matchField, out var cached))
            return Task.FromResult(cached);

        var normalized = NormalizeKey(matchField);
        if (normalized.StartsWith("user.meta.", StringComparison.OrdinalIgnoreCase))
        {
            var key = normalized.Substring("user.meta.".Length);
            var def = new ScanFieldDefinition(normalized, normalized, "user", "string", null, "user_meta",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["key"] = key },
                new[] { "usersMeta" }, null, false, true);
            return Task.FromResult(ResolveUserMetaValue(def, user));
        }

        if (normalized.StartsWith("partner.meta.", StringComparison.OrdinalIgnoreCase))
        {
            var key = normalized.Substring("partner.meta.".Length);
            var def = new ScanFieldDefinition(normalized, normalized, "partner", "string", null, "partner_meta",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["key"] = key },
                new[] { "partnerMeta" }, null, false, true);
            return Task.FromResult(ResolvePartnerMetaValue(def, partnerMeta));
        }

        if (normalized.StartsWith("partner.", StringComparison.OrdinalIgnoreCase))
        {
            var def = new ScanFieldDefinition(normalized, normalized, "partner", "string", null, "partner",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = normalized.Substring("partner.".Length) },
                new[] { "partnerMeta" }, null, false, true);
            return Task.FromResult(ResolvePartnerValue(def, partnerMeta));
        }

        if (normalized.StartsWith("user.", StringComparison.OrdinalIgnoreCase))
        {
            var def = new ScanFieldDefinition(normalized, normalized, "user", "string", null, "profile",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = normalized.Substring(5) },
                new[] { "usersProfile" }, null, false, true);
            return Task.FromResult(ResolveProfileValue(def, user));
        }

        if (normalized.StartsWith("loyalty.", StringComparison.OrdinalIgnoreCase))
        {
            var def = new ScanFieldDefinition(normalized, normalized, "loyalty", "string", null, "loyalty_profile",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = normalized.Substring(8) },
                new[] { "loyaltyProfile" }, null, false, true);
            return Task.FromResult(ResolveLoyaltyValue(def, loyalty));
        }

        if (normalized.StartsWith("meta.", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(normalized, "meta.servertime", StringComparison.OrdinalIgnoreCase))
            {
                var def = new ScanFieldDefinition(normalized, normalized, "meta", "string", null, "meta",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = normalized.Substring(5) },
                    Array.Empty<string>(), null, false, true);
                return Task.FromResult(ResolveMetaValue(def, serverTime));
            }

            var key = normalized.Substring("meta.".Length);
            var defPartner = new ScanFieldDefinition(normalized, normalized, "meta", "string", null, "partner_meta",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["key"] = key },
                new[] { "partnerMeta" }, null, false, true);
            return Task.FromResult(ResolvePartnerMetaValue(defPartner, partnerMeta));
        }

        return Task.FromResult<object?>(null);
    }

    private static object? ReadValue(JsonElement? valueJson, string field)
    {
        if (valueJson is null)
            return null;

        var value = valueJson.Value;
        if (value.ValueKind != JsonValueKind.Object)
            return null;

        if (!value.TryGetProperty(field, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.TryGetInt64(out var l) ? l : prop.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object or JsonValueKind.Array => prop.GetRawText(),
            _ => null
        };
    }

    private static IReadOnlyList<string> ParseFieldList(ScanFieldDefinition def)
    {
        if (!def.ResolverConfig.TryGetValue("fields", out var raw) || string.IsNullOrWhiteSpace(raw))
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

    private static bool TryToNumber(object? value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return false;
            case byte b:
                number = b;
                return true;
            case short s:
                number = s;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case float f:
                number = f;
                return true;
            case double d:
                number = d;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case string text when double.TryParse(text, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static string GetConfigValue(ScanFieldDefinition def, string key)
    {
        return def.ResolverConfig.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string GetLeaf(string key)
    {
        var last = key.LastIndexOf('.') + 1;
        return last > 0 && last < key.Length ? key.Substring(last) : key;
    }

    private static string NormalizeKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}
