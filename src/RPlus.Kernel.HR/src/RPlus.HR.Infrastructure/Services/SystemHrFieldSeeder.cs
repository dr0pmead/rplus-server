using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RPlus.HR.Domain.Entities;
using RPlus.HR.Infrastructure.Persistence;
using System.Text.Json;
using System.Linq;

namespace RPlus.HR.Infrastructure.Services;

public sealed class SystemHrFieldSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemHrFieldSeeder> _logger;

    public SystemHrFieldSeeder(IServiceProvider serviceProvider, ILogger<SystemHrFieldSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

        var systemFields = BuildSystemFields();
        List<HrCustomFieldDefinition> existingFields;
        try
        {
            existingFields = await db.CustomFieldDefinitions
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning("HR custom fields table is missing; skipping system field seed until migrations apply.");
            return;
        }

        var existingMap = existingFields
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;

        foreach (var systemField in systemFields)
        {
            if (existingMap.TryGetValue(systemField.Key, out var existing))
            {
                if (!existing.IsSystem)
                {
                    _logger.LogWarning("HR field key {Key} exists but is not marked as system. Skipping update.", systemField.Key);
                    continue;
                }

                existing.Label = systemField.Label;
                existing.Type = systemField.Type;
                existing.Required = systemField.Required;
                existing.Group = systemField.Group;
                existing.Order = systemField.Order;
                existing.IsActive = systemField.IsActive;
                existing.Pattern = systemField.Pattern;
                existing.MinLength = systemField.MinLength;
                existing.MaxLength = systemField.MaxLength;
                existing.OptionsJson = systemField.OptionsJson;
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
                continue;
            }

            db.CustomFieldDefinitions.Add(systemField);
            created++;
        }

        var allowedKeys = systemFields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "middleName",
            "organization",
            "photo",
            "division",
            "department",
            "position",
            "workEmail",
            "workPhone",
            "employeeNumber",
            "managerId"
        };
        foreach (var staleField in existingFields.Where(x => x.IsSystem && !allowedKeys.Contains(x.Key)))
        {
            if (staleField.IsActive)
            {
                staleField.IsActive = false;
                staleField.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        foreach (var staleField in existingFields.Where(x => !x.IsSystem && legacyKeys.Contains(x.Key)))
        {
            if (staleField.IsActive)
            {
                staleField.IsActive = false;
                staleField.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        if (created > 0 || updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("HR system fields seeded: {Created} created, {Updated} updated", created, updated);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<HrCustomFieldDefinition> BuildSystemFields()
    {
        var now = DateTime.UtcNow;
        var order = 0;

        HrCustomFieldDefinition Field(
            string key,
            string label,
            string type,
            string group,
            string? pattern = null,
            int? minLength = null,
            int? maxLength = null,
            bool required = false,
            string? optionsJson = null)
        {
            return new HrCustomFieldDefinition
            {
                Id = Guid.NewGuid(),
                Key = key,
                Label = label,
                Type = type,
                Required = required,
                Group = group,
                Order = order++,
                IsActive = true,
                IsSystem = true,
                Pattern = pattern,
                MinLength = minLength,
                MaxLength = maxLength,
                OptionsJson = optionsJson,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        static string BuildOptionsJson(
            bool showInCreate = true,
            bool showInEdit = true,
            bool readOnlyCreate = false,
            bool readOnlyEdit = false,
            bool multiple = false,
            string? systemBindingPath = null,
            string? systemBindingTarget = "hr.profile",
            Dictionary<string, object?>? extra = null)
        {
            var payload = new Dictionary<string, object?>
            {
                ["showInCreate"] = showInCreate,
                ["showInEdit"] = showInEdit,
                ["readOnlyCreate"] = readOnlyCreate,
                ["readOnlyEdit"] = readOnlyEdit,
            };

            if (multiple)
                payload["multiple"] = true;

            if (!string.IsNullOrWhiteSpace(systemBindingPath))
            {
                payload["systemBinding"] = new Dictionary<string, string>
                {
                    ["target"] = string.IsNullOrWhiteSpace(systemBindingTarget) ? "hr.profile" : systemBindingTarget,
                    ["path"] = systemBindingPath
                };
            }

            if (extra is not null)
            {
                foreach (var kv in extra)
                {
                    payload[kv.Key] = kv.Value;
                }
            }

            return JsonSerializer.Serialize(payload);
        }

        return new List<HrCustomFieldDefinition>
        {
            Field(
                "login",
                "Login",
                "text",
                "Account",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "login", systemBindingTarget: "users.account")),
            Field(
                "email",
                "Email",
                "text",
                "Account",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "email", systemBindingTarget: "users.account")),
            Field(
                "phone",
                "Phone",
                "text",
                "Account",
                required: true,
                optionsJson: BuildOptionsJson(
                    systemBindingPath: "phone",
                    systemBindingTarget: "users.account",
                    extra: new Dictionary<string, object?>
                    {
                        ["mask"] = "phone.kz"
                    })),
            Field(
                "password",
                "Password",
                "text",
                "Account",
                required: true,
                optionsJson: BuildOptionsJson(
                    showInEdit: false,
                    systemBindingPath: "password",
                    systemBindingTarget: "users.account",
                    extra: new Dictionary<string, object?>
                    {
                        ["isPassword"] = true
                    })),
            Field(
                "firstName",
                "First name",
                "text",
                "HRProfile",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "firstName", systemBindingTarget: "hr.profile")),
            Field(
                "lastName",
                "Last name",
                "text",
                "HRProfile",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "lastName", systemBindingTarget: "hr.profile")),
            Field(
                "iin",
                "IIN",
                "text",
                "HRProfile",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "iin", systemBindingTarget: "hr.profile")),
            Field(
                "birthDate",
                "Birth date",
                "date",
                "HRProfile",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "birthDate", systemBindingTarget: "hr.profile")),
            Field(
                "hireDate",
                "Hire date",
                "date",
                "HRProfile",
                required: true,
                optionsJson: BuildOptionsJson(systemBindingPath: "hireDate", systemBindingTarget: "hr.profile")),
            Field(
                "status",
                "Status",
                "text",
                "HRProfile",
                required: false,
                optionsJson: BuildOptionsJson(
                    showInCreate: false,
                    showInEdit: false,
                    readOnlyCreate: true,
                    readOnlyEdit: true,
                    systemBindingPath: "status",
                    systemBindingTarget: "hr.profile")),
        };
    }
}
