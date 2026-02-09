using System.Text.Json;
using System.Text.Json.Nodes;

namespace RPlus.Meta.Api.Validation;

public sealed record MetaFieldOptionsValidationError(string Code, string Message);

public sealed class MetaFieldOptionsValidationResult
{
    public bool IsValid { get; init; }
    public string? NormalizedJson { get; init; }
    public IReadOnlyList<MetaFieldOptionsValidationError> Errors { get; init; } = Array.Empty<MetaFieldOptionsValidationError>();
}

public static class MetaFieldOptionsValidator
{
    private static readonly HashSet<string> CoreTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text",
        "number",
        "boolean",
        "datetime",
        "select",
        "reference",
        "file",
        "json",
        "system"
    };

    private static readonly Dictionary<string, HashSet<string>> UiPresetsByType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "text", "textarea", "email", "phone", "url", "rich", "masked"
            },
            ["number"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "number", "currency", "percent"
            },
            ["boolean"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "boolean", "switch", "checkbox", "yesno"
            },
            ["datetime"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "datetime", "date", "time", "daterange"
            },
            ["select"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "select", "autocomplete", "multiselect", "tags"
            },
            ["reference"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reference", "entity", "user", "partner"
            },
            ["file"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "file", "image", "gallery", "document"
            },
            ["json"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "json", "address", "contacts"
            },
            ["system"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "system", "uid"
            }
        };

    private static readonly HashSet<string> SourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "static",
        "dictionary",
        "entity",
        "external"
    };

    public static MetaFieldOptionsValidationResult Validate(string? optionsJson, string dataType)
    {
        var errors = new List<MetaFieldOptionsValidationError>();
        var normalizedType = (dataType ?? "text").Trim().ToLowerInvariant();

        if (!CoreTypes.Contains(normalizedType))
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_FIELD_TYPE", $"Type '{dataType}' is not supported."));
            return new MetaFieldOptionsValidationResult { IsValid = false, Errors = errors };
        }

        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            if (string.Equals(normalizedType, "system", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new JsonObject
                {
                    ["advanced"] = true
                };
                return new MetaFieldOptionsValidationResult
                {
                    IsValid = true,
                    NormalizedJson = payload.ToJsonString()
                };
            }

            return new MetaFieldOptionsValidationResult
            {
                IsValid = true,
                NormalizedJson = null
            };
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(optionsJson);
        }
        catch
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_OPTIONS_JSON", "OptionsJson is not valid JSON."));
            return new MetaFieldOptionsValidationResult { IsValid = false, Errors = errors };
        }

        if (root is not JsonObject obj)
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_OPTIONS_OBJECT", "OptionsJson must be a JSON object."));
            return new MetaFieldOptionsValidationResult { IsValid = false, Errors = errors };
        }

        if (TryGetString(obj["type"], out var explicitType))
        {
            if (!CoreTypes.Contains(explicitType!))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_OPTIONS_TYPE", $"OptionsJson.type '{explicitType}' is not supported."));
            }
            else if (!string.Equals(explicitType, normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new MetaFieldOptionsValidationError("TYPE_MISMATCH", $"OptionsJson.type '{explicitType}' does not match field type '{normalizedType}'."));
            }
        }

        if (TryGetString(obj["uiPreset"], out var uiPreset))
        {
            if (!UiPresetsByType.TryGetValue(normalizedType, out var allowedPresets) || !allowedPresets.Contains(uiPreset!))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_UI_PRESET_FOR_TYPE", $"uiPreset '{uiPreset}' is not allowed for type '{normalizedType}'."));
            }
        }

        if (obj["constraints"] is JsonObject constraints)
        {
            if (constraints.TryGetPropertyValue("required", out var requiredNode)
                && !TryGetBool(requiredNode, out _))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_REQUIRED", "constraints.required must be boolean."));
            }

            if (constraints.TryGetPropertyValue("min", out var minNode)
                && !IsMinMaxAllowed(normalizedType))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_MIN", $"constraints.min is not applicable to '{normalizedType}'."));
            }
            else if (constraints.TryGetPropertyValue("min", out minNode)
                && !IsValidMinMaxValue(normalizedType, minNode))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_MIN", "constraints.min has invalid value."));
            }

            if (constraints.TryGetPropertyValue("max", out var maxNode)
                && !IsMinMaxAllowed(normalizedType))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_MAX", $"constraints.max is not applicable to '{normalizedType}'."));
            }
            else if (constraints.TryGetPropertyValue("max", out maxNode)
                && !IsValidMinMaxValue(normalizedType, maxNode))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_MAX", "constraints.max has invalid value."));
            }

            if (constraints.TryGetPropertyValue("regex", out var regexNode))
            {
                if (!string.Equals(normalizedType, "text", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_REGEX", "constraints.regex is only allowed for text fields."));
                }
                else if (!TryGetString(regexNode, out _))
                {
                    errors.Add(new MetaFieldOptionsValidationError("INVALID_CONSTRAINT_REGEX", "constraints.regex must be a string."));
                }
            }
        }

        var hasLegacyValues = obj.TryGetPropertyValue("values", out var legacyValuesNode) && legacyValuesNode is JsonArray;
        if (legacyValuesNode is JsonArray legacyValuesArray)
        {
            foreach (var item in legacyValuesArray)
            {
                if (!TryGetString(item, out _))
                {
                    errors.Add(new MetaFieldOptionsValidationError("INVALID_VALUES", "values must contain only strings."));
                    break;
                }
            }
        }
        else if (obj.TryGetPropertyValue("values", out legacyValuesNode) && legacyValuesNode is not null)
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_VALUES", "values must be an array."));
        }

        if (obj.TryGetPropertyValue("multiple", out var legacyMultipleNode)
            && !TryGetBool(legacyMultipleNode, out _))
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_MULTIPLE", "multiple must be boolean."));
        }

        if (obj.TryGetPropertyValue("ui", out var uiNode) && uiNode is JsonObject uiObj)
        {
            ValidateUiFlag(uiObj, "showInCreate", errors);
            ValidateUiFlag(uiObj, "showInEdit", errors);
            ValidateUiFlag(uiObj, "readOnlyCreate", errors);
            ValidateUiFlag(uiObj, "readOnlyEdit", errors);
            ValidateUiFlag(uiObj, "showInLink", errors);
        }
        else if (obj.TryGetPropertyValue("ui", out uiNode) && uiNode is not null)
        {
            errors.Add(new MetaFieldOptionsValidationError("INVALID_UI_OBJECT", "ui must be an object."));
        }

        if (obj["behavior"] is JsonObject behavior)
        {
            if (behavior.TryGetPropertyValue("multiple", out var multipleNode)
                && !TryGetBool(multipleNode, out _))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_BEHAVIOR_MULTIPLE", "behavior.multiple must be boolean."));
            }

            if (string.Equals(normalizedType, "select", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedType, "reference", StringComparison.OrdinalIgnoreCase))
            {
                if (behavior.TryGetPropertyValue("source", out var sourceNode) && sourceNode is JsonObject source)
                {
                    if (!TryGetString(source["type"], out var sourceType) || string.IsNullOrWhiteSpace(sourceType))
                    {
                        errors.Add(new MetaFieldOptionsValidationError("INVALID_SOURCE_TYPE", "behavior.source.type is required."));
                    }
                    else if (!SourceTypes.Contains(sourceType!))
                    {
                        errors.Add(new MetaFieldOptionsValidationError("INVALID_SOURCE_TYPE", $"behavior.source.type '{sourceType}' is not supported."));
                    }
                    else if (string.Equals(sourceType, "static", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!source.TryGetPropertyValue("options", out var optionsNode) || optionsNode is not JsonArray)
                        {
                            errors.Add(new MetaFieldOptionsValidationError("SOURCE_OPTIONS_REQUIRED", "behavior.source.options must be an array for static sources."));
                        }
                    }
                    else if (string.Equals(sourceType, "entity", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryGetString(source["entity"], out _))
                        {
                            errors.Add(new MetaFieldOptionsValidationError("SOURCE_ENTITY_REQUIRED", "behavior.source.entity is required for entity sources."));
                        }
                    }
                }
            }
        }
        else if (string.Equals(normalizedType, "select", StringComparison.OrdinalIgnoreCase) && !hasLegacyValues)
        {
            // Select fields can rely on referenceSourceJson outside of options or legacy values list.
            // We only enforce behavior.source when behavior is explicitly provided.
        }

        if (obj.TryGetPropertyValue("advanced", out var advancedNode))
        {
            if (!TryGetBool(advancedNode, out _))
            {
                errors.Add(new MetaFieldOptionsValidationError("INVALID_ADVANCED_FLAG", "advanced must be boolean."));
            }
        }

        if (string.Equals(normalizedType, "system", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetBool(obj["advanced"], out var advanced) || !advanced)
            {
                obj["advanced"] = true;
            }
        }

        if (errors.Count > 0)
        {
            return new MetaFieldOptionsValidationResult
            {
                IsValid = false,
                Errors = errors
            };
        }

        return new MetaFieldOptionsValidationResult
        {
            IsValid = true,
            NormalizedJson = obj.Count == 0 ? null : obj.ToJsonString()
        };
    }

    private static bool IsMinMaxAllowed(string type) =>
        string.Equals(type, "text", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "number", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "datetime", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidMinMaxValue(string type, JsonNode? node)
    {
        if (node is null) return false;

        if (string.Equals(type, "datetime", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetNumber(node, out _) || TryGetString(node, out _);
        }

        return TryGetNumber(node, out _);
    }

    private static bool TryGetString(JsonNode? node, out string? value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var str))
        {
            value = str;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetBool(JsonNode? node, out bool value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetNumber(JsonNode? node, out double value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out var numValue))
        {
            value = numValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static void ValidateUiFlag(JsonObject uiObj, string key, List<MetaFieldOptionsValidationError> errors)
    {
        if (!uiObj.TryGetPropertyValue(key, out var node))
            return;

        if (!TryGetBool(node, out _))
            errors.Add(new MetaFieldOptionsValidationError("INVALID_UI_FLAG", $"ui.{key} must be boolean."));
    }
}
