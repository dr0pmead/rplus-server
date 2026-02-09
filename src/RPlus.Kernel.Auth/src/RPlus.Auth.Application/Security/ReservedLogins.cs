using System;
using System.Collections.Generic;

namespace RPlus.Auth.Application.Security;

public static class ReservedLogins
{
    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "root", "sysadmin", "system", "superuser",
        "owner", "host", "master", "support", "security", "audit", "bot"
    };

    public static bool IsReserved(string? login)
    {
        if (string.IsNullOrWhiteSpace(login))
            return false;

        var normalized = login.Trim();
        return Values.Contains(normalized);
    }
}

