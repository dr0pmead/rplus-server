using System.Text.RegularExpressions;

namespace RPlus.SDK.Security.Helpers;

public static class GuardRedisKeys
{
    private static string Sanitize(string input) => Regex.Replace(input, "[^a-zA-Z0-9_-]", "");

    public static string Threat(string subject) => $"guard:threat:{Sanitize(subject)}";
    public static string Block(string subject) => $"guard:block:{Sanitize(subject)}";
    public static string Rate(string subject, string route) => $"guard:rate:{Sanitize(subject)}:{Sanitize(route)}";
    public static string Challenge(string challengeId) => $"guard:challenge:{Sanitize(challengeId)}";
    public static string SignalDedup(string subject, string signalType, long timeBucket) => $"guard:signal:{Sanitize(subject)}:{Sanitize(signalType)}:{timeBucket}";
}
