using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace RPlus.SDK.Gateway.Realtime;

public static class RealtimeRegistryBuilder
{
    public static IReadOnlyCollection<RealtimeEventDescriptor> BuildRegistry(
        IReadOnlyDictionary<string, RealtimeMappingDefinition> mappings,
        IReadOnlySet<string> grantedPermissions)
    {
        if (mappings.Count == 0)
            return [];

        var list = new List<RealtimeEventDescriptor>();
        foreach (var kv in mappings)
        {
            var mapping = kv.Value;
            if (!IsAllowed(mapping, grantedPermissions))
                continue;

            list.Add(mapping.ToDescriptor());
        }

        return list
            .Distinct()
            .OrderBy(x => x.Type, System.StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsAllowed(RealtimeMappingDefinition mapping, IReadOnlySet<string> grantedPermissions)
    {
        if (string.IsNullOrWhiteSpace(mapping.RequiredPermission))
            return true;

        return grantedPermissions.Contains(mapping.RequiredPermission);
    }
}

