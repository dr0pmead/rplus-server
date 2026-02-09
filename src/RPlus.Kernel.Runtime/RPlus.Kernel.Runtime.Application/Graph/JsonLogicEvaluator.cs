using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace RPlus.Kernel.Runtime.Application.Graph;

/// <summary>
/// Minimal JsonLogic-compatible evaluator (subset) used by Loyalty v2 graph rules.
/// Supported operators: var, ==, !=, &gt;, &gt;=, &lt;, &lt;=, and, or, !, if, in.
/// Property lookup for <c>var</c> is case-insensitive.
/// </summary>
public static class JsonLogicEvaluator
{
    public static bool EvaluateBoolean(JsonElement logic, JsonElement data)
        => IsTruthy(Evaluate(logic, data));

    public static object? Evaluate(JsonElement logic, JsonElement data)
    {
        return logic.ValueKind switch
        {
            JsonValueKind.Object => EvaluateObject(logic, data),
            JsonValueKind.Array => logic.EnumerateArray().Select(x => Evaluate(x, data)).ToArray(),
            JsonValueKind.String => logic.GetString(),
            JsonValueKind.Number => logic.TryGetInt64(out var i) ? i : logic.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static object? EvaluateObject(JsonElement obj, JsonElement data)
    {
        using var enumerator = obj.EnumerateObject();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var first = enumerator.Current;
        if (enumerator.MoveNext())
        {
            return obj;
        }

        var op = first.Name;
        var args = first.Value;

        return op switch
        {
            "var" => EvaluateVar(args, data),
            "==" => EvaluateEquals(args, data),
            "!=" => !EvaluateEquals(args, data),
            ">" => CompareNumeric(args, data, (a, b) => a > b),
            ">=" => CompareNumeric(args, data, (a, b) => a >= b),
            "<" => CompareNumeric(args, data, (a, b) => a < b),
            "<=" => CompareNumeric(args, data, (a, b) => a <= b),
            "and" => EvaluateAnd(args, data),
            "or" => EvaluateOr(args, data),
            "!" => !IsTruthy(Evaluate(FirstArg(args), data)),
            "if" => EvaluateIf(args, data),
            "in" => EvaluateIn(args, data),
            _ => null
        };
    }

    private static JsonElement FirstArg(JsonElement args)
    {
        if (args.ValueKind == JsonValueKind.Array && args.GetArrayLength() > 0)
        {
            return args[0];
        }

        return args;
    }

    private static object? EvaluateVar(JsonElement args, JsonElement data)
    {
        if (args.ValueKind == JsonValueKind.String)
        {
            return ResolveVar(data, args.GetString()!);
        }

        if (args.ValueKind == JsonValueKind.Array)
        {
            var len = args.GetArrayLength();
            if (len == 0)
            {
                return null;
            }

            var path = args[0].ValueKind == JsonValueKind.String ? args[0].GetString() : null;
            var fallback = len > 1 ? Evaluate(args[1], data) : null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            var value = ResolveVar(data, path);
            return value ?? fallback;
        }

        return null;
    }

    private static object EvaluateAnd(JsonElement args, JsonElement data)
    {
        if (args.ValueKind != JsonValueKind.Array)
        {
            return IsTruthy(Evaluate(args, data));
        }

        object? last = null;
        foreach (var arg in args.EnumerateArray())
        {
            last = Evaluate(arg, data);
            if (!IsTruthy(last))
            {
                return false;
            }
        }

        return IsTruthy(last);
    }

    private static object EvaluateOr(JsonElement args, JsonElement data)
    {
        if (args.ValueKind != JsonValueKind.Array)
        {
            return IsTruthy(Evaluate(args, data));
        }

        foreach (var arg in args.EnumerateArray())
        {
            var value = Evaluate(arg, data);
            if (IsTruthy(value))
            {
                return true;
            }
        }

        return false;
    }

    private static object? EvaluateIf(JsonElement args, JsonElement data)
    {
        if (args.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var len = args.GetArrayLength();
        for (var i = 0; i + 1 < len; i += 2)
        {
            var cond = Evaluate(args[i], data);
            if (IsTruthy(cond))
            {
                return Evaluate(args[i + 1], data);
            }
        }

        return len % 2 == 1 ? Evaluate(args[len - 1], data) : null;
    }

    private static object EvaluateIn(JsonElement args, JsonElement data)
    {
        if (args.ValueKind != JsonValueKind.Array || args.GetArrayLength() < 2)
        {
            return false;
        }

        var needle = Evaluate(args[0], data);
        var haystack = Evaluate(args[1], data);

        if (needle is JsonElement ne)
        {
            needle = UnwrapJsonElement(ne);
        }

        if (haystack is JsonElement he)
        {
            haystack = he;
        }

        if (haystack is string hs)
        {
            var ns = needle?.ToString() ?? string.Empty;
            return hs.Contains(ns, StringComparison.Ordinal);
        }

        if (haystack is object?[] arr)
        {
            return arr.Any(x => AreEqual(x, needle));
        }

        if (haystack is JsonElement json && json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray())
            {
                if (AreEqual(UnwrapJsonElement(item), needle))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool EvaluateEquals(JsonElement args, JsonElement data)
    {
        if (args.ValueKind != JsonValueKind.Array || args.GetArrayLength() < 2)
        {
            return false;
        }

        var left = Evaluate(args[0], data);
        var right = Evaluate(args[1], data);

        return AreEqual(left, right);
    }

    private static bool CompareNumeric(JsonElement args, JsonElement data, Func<double, double, bool> numericComparator)
    {
        if (args.ValueKind != JsonValueKind.Array || args.GetArrayLength() < 2)
        {
            return false;
        }

        var left = Evaluate(args[0], data);
        var right = Evaluate(args[1], data);

        var ln = AsNumber(left);
        var rn = AsNumber(right);
        if (ln is null || rn is null)
        {
            return false;
        }

        return numericComparator(ln.Value, rn.Value);
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is JsonElement le)
        {
            left = UnwrapJsonElement(le);
        }

        if (right is JsonElement re)
        {
            right = UnwrapJsonElement(re);
        }

        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        var ln = AsNumber(left);
        var rn = AsNumber(right);
        if (ln is not null && rn is not null)
        {
            return Math.Abs(ln.Value - rn.Value) < 0.0000001;
        }

        return string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    private static double? AsNumber(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement el)
        {
            value = UnwrapJsonElement(el);
            if (value is null)
            {
                return null;
            }
        }

        return value switch
        {
            byte b => b,
            short s => s,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            decimal m => (double)m,
            bool bo => bo ? 1 : 0,
            string str => double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ => null
        };
    }

    private static bool IsTruthy(object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is JsonElement el)
        {
            value = UnwrapJsonElement(el);
        }

        return value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s),
            byte b => b != 0,
            short s => s != 0,
            int i => i != 0,
            long l => l != 0,
            float f => Math.Abs(f) > 0.0000001,
            double d => Math.Abs(d) > 0.0000001,
            decimal m => m != 0,
            object?[] arr => arr.Length > 0,
            _ => true
        };
    }

    private static object? UnwrapJsonElement(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el
        };
    }

    private static object? ResolveVar(JsonElement data, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return data;
        }

        var current = data;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!TryGetPropertyCaseInsensitive(current, segment, out var next))
                {
                    return null;
                }

                current = next;
                continue;
            }

            if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                if (index < 0 || index >= current.GetArrayLength())
                {
                    return null;
                }

                current = current[index];
                continue;
            }

            return null;
        }

        return current;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

