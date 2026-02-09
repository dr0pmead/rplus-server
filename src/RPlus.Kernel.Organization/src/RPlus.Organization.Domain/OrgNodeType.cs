using System;
using System.Collections.Generic;

namespace RPlus.Organization.Domain;

public enum OrgNodeType
{
    Group = 10,
    Organization = 20,
    Division = 30,
    Department = 40,
    Team = 50
}

public static class OrgNodeTypeRules
{
    private static readonly IReadOnlyDictionary<OrgNodeType, OrgNodeType[]> AllowedParents = new Dictionary<OrgNodeType, OrgNodeType[]>
    {
        [OrgNodeType.Group] = Array.Empty<OrgNodeType>(),
        [OrgNodeType.Organization] = new[] { OrgNodeType.Group },
        [OrgNodeType.Division] = new[] { OrgNodeType.Organization },
        [OrgNodeType.Department] = new[] { OrgNodeType.Division, OrgNodeType.Organization },
        [OrgNodeType.Team] = new[] { OrgNodeType.Department }
    };

    public static bool TryParse(string? raw, out OrgNodeType type) =>
        Enum.TryParse(raw, ignoreCase: true, out type);

    public static string NormalizeToStorage(OrgNodeType type) =>
        type.ToString().ToLowerInvariant();

    public static bool IsValidParent(OrgNodeType child, OrgNodeType? parent)
    {
        if (child == OrgNodeType.Group)
        {
            return parent == null;
        }

        if (child == OrgNodeType.Organization)
        {
            return parent == null || parent == OrgNodeType.Group;
        }

        if (parent == null)
        {
            return false;
        }

        return AllowedParents.TryGetValue(child, out var allowed) && Array.Exists(allowed, p => p == parent.Value);
    }

    public static IReadOnlyList<OrgNodeType> GetAllowedChildren(OrgNodeType parent) =>
        parent switch
        {
            OrgNodeType.Group => new[] { OrgNodeType.Organization },
            OrgNodeType.Organization => new[] { OrgNodeType.Division, OrgNodeType.Department },
            OrgNodeType.Division => new[] { OrgNodeType.Department },
            OrgNodeType.Department => new[] { OrgNodeType.Team },
            _ => Array.Empty<OrgNodeType>()
        };

}
