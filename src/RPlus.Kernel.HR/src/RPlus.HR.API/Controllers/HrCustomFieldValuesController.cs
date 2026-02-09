using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.HR.Api.Authorization;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Domain.Entities;
using RPlus.SDK.Access.Authorization;
using RPlus.SDK.Eventing.Abstractions;
using RPlus.HR.Api.Events;

namespace RPlus.HR.Api.Controllers;

[ApiController]
[Route("api/hr/users/{userId:guid}/fields")]
[Authorize]
public sealed class HrCustomFieldValuesController : ControllerBase
{
    private readonly IHrDbContext _db;
    private readonly IEventPublisher _events;
    private readonly IHrActorContext _actor;

    public HrCustomFieldValuesController(IHrDbContext db, IEventPublisher events, IHrActorContext actor)
    {
        _db = db;
        _events = events;
        _actor = actor;
    }

    [HttpGet]
    [AllowSelf]
    [RequiresPermission("hr.profile.view")]
    public async Task<IActionResult> GetUserFields(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return BadRequest(new { error = "invalid_user_id" });

        var definitions = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);

        var values = await _db.CustomFieldValues
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var valueMap = values.ToDictionary(x => x.FieldKey, x => x.ValueJson, StringComparer.OrdinalIgnoreCase);
        var response = new UserCustomFieldsResponse(
            definitions.Select(MapDefinition).ToArray(),
            valueMap);

        return Ok(response);
    }

    [HttpPost]
    [AllowSelf]
    [RequiresPermission("hr.profile.edit")]
    public async Task<IActionResult> UpsertUserFields(Guid userId, [FromBody] UpsertCustomFieldValuesRequest request, CancellationToken ct)
    {
        if (userId == Guid.Empty || request?.Items == null)
            return BadRequest(new { error = "invalid_request" });

        var items = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldKey))
            .Select(x => new UpsertCustomFieldValueRequest(x.FieldKey.Trim(), x.Value))
            .ToArray();

        if (items.Length == 0)
            return BadRequest(new { error = "empty_payload" });

        var keys = items.Select(x => x.FieldKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var definitions = await _db.CustomFieldDefinitions
            .Where(x => keys.Contains(x.Key))
            .ToListAsync(ct);

        if (definitions.Count != keys.Length)
            return BadRequest(new { error = "unknown_field" });

        var now = DateTime.UtcNow;
        var existingValues = await _db.CustomFieldValues
            .Where(x => x.UserId == userId && keys.Contains(x.FieldKey))
            .ToListAsync(ct);
        var existingMap = existingValues.ToDictionary(x => x.FieldKey, StringComparer.OrdinalIgnoreCase);

        var filledEvents = new List<HrCustomFieldFilledEvent>();

        foreach (var item in items)
        {
            var definition = definitions.First(x => x.Key.Equals(item.FieldKey, StringComparison.OrdinalIgnoreCase));
            if (!IsValueAllowed(definition, item.Value))
                return BadRequest(new { error = "invalid_value", field = definition.Key });

            var valueJson = item.Value.HasValue ? item.Value.Value.GetRawText() : "null";
            existingMap.TryGetValue(definition.Key, out var existing);
            var wasFilled = IsFilled(definition, existing?.ValueJson);
            var isFilled = IsFilled(definition, valueJson);
            if (existing == null)
            {
                existing = new HrCustomFieldValue
                {
                    UserId = userId,
                    FieldKey = definition.Key,
                    ValueJson = valueJson,
                    UpdatedAt = now
                };
                _db.CustomFieldValues.Add(existing);
            }
            else
            {
                existing.ValueJson = valueJson;
                existing.UpdatedAt = now;
            }

            if (!wasFilled && isFilled)
            {
                filledEvents.Add(new HrCustomFieldFilledEvent(
                    userId,
                    definition.Key,
                    definition.Required,
                    GetEntityFromGroup(definition.Group),
                    _actor.ActorUserId,
                    _actor.ActorType,
                    _actor.ActorService,
                    now));
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var evt in filledEvents)
        {
            await _events.PublishAsync(evt, HrCustomFieldFilledEvent.EventName, evt.UserId.ToString(), ct);
        }

        return Ok(new { ok = true });
    }

    private static string GetEntityFromGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return "employee";

        var idx = group.IndexOf('/', StringComparison.Ordinal);
        return idx > 0 ? group[..idx] : group;
    }

    private static bool IsFilled(HrCustomFieldDefinition definition, string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson) || valueJson == "null")
            return false;

        try
        {
            using var doc = JsonDocument.Parse(valueJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Null || root.ValueKind == JsonValueKind.Undefined)
                return false;

            if (root.ValueKind == JsonValueKind.String)
            {
                var str = root.GetString();
                return !string.IsNullOrWhiteSpace(str);
            }

            if (root.ValueKind == JsonValueKind.Array)
                return root.GetArrayLength() > 0;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValueAllowed(HrCustomFieldDefinition definition, JsonElement? value)
    {
        if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Null)
            return !definition.Required;

        var options = ParseOptions(definition.OptionsJson);
        var isMultiple = options.Multiple;
        var allowed = options.AllowedValues;

        if (isMultiple)
        {
            if (value.Value.ValueKind != JsonValueKind.Array)
                return false;

            if (definition.Required && value.Value.GetArrayLength() == 0)
                return false;

            foreach (var item in value.Value.EnumerateArray())
            {
                if (!IsScalarAllowed(definition, item, allowed))
                    return false;
            }

            return true;
        }

        if (value.Value.ValueKind == JsonValueKind.Array)
            return false;

        return IsScalarAllowed(definition, value.Value, allowed);
    }

    private static bool IsScalarAllowed(HrCustomFieldDefinition definition, JsonElement value, HashSet<string>? allowed)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return !definition.Required;

        switch (definition.Type)
        {
            case "number":
                if (value.ValueKind == JsonValueKind.Number)
                    return true;
                if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out _))
                    return true;
                return false;
            case "checkbox":
                return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
            case "date":
                return value.ValueKind == JsonValueKind.String;
            case "select":
                if (value.ValueKind != JsonValueKind.String && value.ValueKind != JsonValueKind.Number)
                    return false;
                if (allowed == null || allowed.Count == 0)
                    return true;
                return allowed.Contains(value.ToString() ?? string.Empty);
            case "file":
                return value.ValueKind == JsonValueKind.String;
            case "textarea":
            case "text":
            default:
                if (value.ValueKind != JsonValueKind.String)
                    return false;
                var str = value.GetString() ?? string.Empty;
                if (definition.Required && string.IsNullOrWhiteSpace(str))
                    return false;
                if (definition.MinLength.HasValue && str.Length < definition.MinLength.Value)
                    return false;
                if (definition.MaxLength.HasValue && str.Length > definition.MaxLength.Value)
                    return false;
                if (!string.IsNullOrWhiteSpace(definition.Pattern))
                {
                    if (!Regex.IsMatch(str, definition.Pattern))
                        return false;
                }
                return true;
        }
    }

    private sealed record ParsedOptions(bool Multiple, HashSet<string>? AllowedValues);

    private static ParsedOptions ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new ParsedOptions(false, null);

        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var multiple = doc.RootElement.TryGetProperty("multiple", out var multipleProp)
                    && multipleProp.ValueKind == JsonValueKind.True;

                HashSet<string>? allowed = null;
                if (doc.RootElement.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
                {
                    allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in optionsProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String || item.ValueKind == JsonValueKind.Number)
                        {
                            allowed.Add(item.ToString() ?? string.Empty);
                        }
                        else if (item.ValueKind == JsonValueKind.Object)
                        {
                            if (item.TryGetProperty("value", out var valueProp))
                                allowed.Add(valueProp.ToString() ?? string.Empty);
                            else if (item.TryGetProperty("label", out var labelProp))
                                allowed.Add(labelProp.ToString() ?? string.Empty);
                            else if (item.TryGetProperty("content", out var contentProp))
                                allowed.Add(contentProp.ToString() ?? string.Empty);
                        }
                    }
                }
                return new ParsedOptions(multiple, allowed);
            }
        }
        catch
        {
            // ignore
        }

        return new ParsedOptions(false, null);
    }

    private static CustomFieldDefinitionDto MapDefinition(HrCustomFieldDefinition field) => new(
        field.Id,
        field.Key,
        field.Label,
        field.Type,
        field.Required,
        field.Group,
        field.Order,
        field.IsActive,
        field.IsSystem,
        field.MinLength,
        field.MaxLength,
        field.Pattern,
        field.Placeholder,
        field.OptionsJson);

    public sealed record CustomFieldDefinitionDto(
        Guid Id,
        string Key,
        string Label,
        string Type,
        bool Required,
        string Group,
        int Order,
        bool IsActive,
        bool IsSystem,
        int? MinLength,
        int? MaxLength,
        string? Pattern,
        string? Placeholder,
        string? OptionsJson);

    public sealed record UserCustomFieldsResponse(
        CustomFieldDefinitionDto[] Definitions,
        IDictionary<string, string> Values);

    public sealed record UpsertCustomFieldValuesRequest(UpsertCustomFieldValueRequest[] Items);

    public sealed record UpsertCustomFieldValueRequest(string FieldKey, JsonElement? Value);
}
