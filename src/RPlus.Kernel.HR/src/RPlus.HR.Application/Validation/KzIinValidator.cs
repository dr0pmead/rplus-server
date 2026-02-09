namespace RPlus.HR.Application.Validation;

public static class KzIinValidator
{
    /// <summary>Validates Kazakhstan IIN (12 digits) using Mod 11 checksum (two-pass weights).</summary>
    public static bool IsValid(string? iin)
    {
        if (string.IsNullOrWhiteSpace(iin))
            return false;

        var s = iin.Trim();
        if (s.Length != 12)
            return false;

        Span<int> digits = stackalloc int[12];
        for (var i = 0; i < 12; i++)
        {
            var ch = s[i];
            if (ch is < '0' or > '9')
                return false;
            digits[i] = ch - '0';
        }

        var expected = digits[11];

        Span<int> w1 = stackalloc int[11] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
        Span<int> w2 = stackalloc int[11] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 1, 2 };

        var checksum = ComputeChecksum(digits, w1);
        if (checksum == 10)
            checksum = ComputeChecksum(digits, w2);

        if (checksum == 10)
            return false;

        return checksum == expected;
    }

    private static int ComputeChecksum(ReadOnlySpan<int> digits, ReadOnlySpan<int> weights)
    {
        var sum = 0;
        for (var i = 0; i < 11; i++)
            sum += digits[i] * weights[i];
        return sum % 11;
    }
}

