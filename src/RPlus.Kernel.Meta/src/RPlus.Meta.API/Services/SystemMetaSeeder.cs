using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RPlus.Meta.Domain.Entities;
using RPlus.Meta.Infrastructure.Persistence;
using RPlus.Meta.Api.Validation;

namespace RPlus.Meta.Api.Services;

public sealed class SystemMetaSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemMetaSeeder> _logger;

    public SystemMetaSeeder(IServiceProvider serviceProvider, ILogger<SystemMetaSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetaDbContext>();

        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS meta;", cancellationToken);

        var entityTypes = new List<(string Key, string Title, string Description)>
        {
            ("user", "User", "Primary identity and account profile."),
            ("employee", "Employee", "HR profile data for employees."),
            ("organization", "Organization", "Organization registry entries."),
            ("department", "Department", "Departments registry entries."),
            ("position", "Position", "Positions and roles registry entries."),
            ("integration_partner", "Integration Partner", "Partner/company profile for integrations.")
        };


        var now = DateTime.UtcNow;
        foreach (var (key, title, description) in entityTypes)
        {
            var existing = await db.EntityTypes.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (existing == null)
            {
                db.EntityTypes.Add(new MetaEntityType
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Title = title,
                    Description = description,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = now
                });
                continue;
            }

            if (existing.IsSystem)
            {
                existing.Title = title;
                existing.Description = description;
                existing.IsActive = true;
            }
        }

        await SeedFieldTypesAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var types = await db.EntityTypes.AsNoTracking().ToListAsync(cancellationToken);
        var byKey = types.ToDictionary(x => x.Key, x => x.Id);

        await SeedListsAsync(db, cancellationToken);
        await SeedSystemListFieldsAsync(db, cancellationToken);
        await SeedNodeTemplatesAsync(db, cancellationToken);
        await SeedScanFieldsAsync(db, cancellationToken);
        await SeedProfileTabsAsync(db, cancellationToken);
        
        await SeedFieldsAsync(db, byKey, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedFieldsAsync(MetaDbContext db, Dictionary<string, Guid> typeIds, CancellationToken ct)
    {
        var userTypeId = typeIds["user"];
        var employeeTypeId = typeIds["employee"];
        var organizationTypeId = typeIds["organization"];
        var departmentTypeId = typeIds["department"];
        var positionTypeId = typeIds["position"];
        var integrationPartnerTypeId = typeIds["integration_partner"];

        var fields = new List<MetaFieldDefinition>
        {
            // User (identity)
            SystemField(userTypeId, "userId", "User ID", "system", 0, true),
            SystemField(userTypeId, "login", "Login", "text", 1, true),
            SystemField(userTypeId, "email", "Email", "text", 2, false),
            SystemField(userTypeId, "phone", "Phone", "text", 3, false),
            SystemField(userTypeId, "firstName", "First name", "text", 4, false),
            SystemField(userTypeId, "lastName", "Last name", "text", 5, false),
            SystemField(userTypeId, "middleName", "Middle name", "text", 6, false),
            SystemField(userTypeId, "status", "Status", "text", 7, false),
            SystemField(userTypeId, "createdAt", "Created", "datetime", 90, true),
            SystemField(userTypeId, "updatedAt", "Updated", "datetime", 91, true),

            // Employee (HR)
            SystemField(employeeTypeId, "hireDate", "Hire date", "datetime", 0, false, null, BuildOptionsJson(null, "date")),
            SystemField(employeeTypeId, "workEmail", "Work email", "text", 5, false),
            SystemField(employeeTypeId, "workPhone", "Work phone", "text", 6, false),
            SystemField(employeeTypeId, "employeeNumber", "Employee number", "text", 7, false),
            SystemField(employeeTypeId, "managerId", "Manager", "reference", 8, false, ReferenceEntity("user")),

            // Organization
            SystemField(organizationTypeId, "name", "Name", "text", 0, true),
            SystemField(organizationTypeId, "code", "Code", "text", 1, false),
            SystemField(organizationTypeId, "status", "Status", "text", 2, false),

            // Department
            SystemField(departmentTypeId, "name", "Name", "text", 0, true),
            SystemField(departmentTypeId, "code", "Code", "text", 1, false),

            // Position
            SystemField(positionTypeId, "title", "Title", "text", 0, true),
            SystemField(positionTypeId, "code", "Code", "text", 1, false),
            SystemField(positionTypeId, "grade", "Grade", "text", 3, false),

            // Integration partners (system default)
            SystemField(
                integrationPartnerTypeId,
                "name",
                "Name",
                "text",
                0,
                true,
                null,
                BuildOptionsJson("Display name used in UI and reports.", "text", null, new { required = true }),
                true),
        };

        foreach (var field in fields)
        {
            var optionsResult = MetaFieldOptionsValidator.Validate(field.OptionsJson, field.DataType);
            if (!optionsResult.IsValid)
            {
                var summary = string.Join(", ", optionsResult.Errors.Select(x => x.Code));
                throw new InvalidOperationException($"Invalid OptionsJson for field '{field.Key}': {summary}");
            }

            field.OptionsJson = optionsResult.NormalizedJson;

            var exists = await db.FieldDefinitions.AnyAsync(
                x => x.EntityTypeId == field.EntityTypeId && x.Key == field.Key,
                ct);

            if (exists)
                continue;

            db.FieldDefinitions.Add(field);
        }

        await db.SaveChangesAsync(ct);


        var integrationKeys = new List<string> { "name" };

        var integrationSystemFields = await db.FieldDefinitions
            .Where(x => x.EntityTypeId == integrationPartnerTypeId && x.IsSystem)
            .ToListAsync(ct);

        var updated = false;
        foreach (var field in integrationSystemFields)
        {
            if (!integrationKeys.Contains(field.Key))
            {
                db.FieldDefinitions.Remove(field);
                updated = true;
                continue;
            }

            var updatedOptions = BuildOptionsJson("Display name used in UI and reports.", "text", null, new { required = true });
            var normalizedType = "text";

            if (!field.IsSystem)
            {
                field.IsSystem = true;
                updated = true;
            }

            if (!string.Equals(field.DataType, normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                field.DataType = normalizedType;
                updated = true;
            }

            var optionsResult = MetaFieldOptionsValidator.Validate(updatedOptions, normalizedType);
            if (!optionsResult.IsValid)
            {
                var summary = string.Join(", ", optionsResult.Errors.Select(x => x.Code));
                throw new InvalidOperationException($"Invalid OptionsJson for field '{field.Key}': {summary}");
            }

            var normalizedOptions = optionsResult.NormalizedJson;

            if (!string.Equals(field.OptionsJson, normalizedOptions, StringComparison.OrdinalIgnoreCase))
            {
                field.OptionsJson = normalizedOptions;
                updated = true;
            }
        }

        var blockedPartnerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "skidka_partnera",
            "discountpartner"
        };

        var blockedPartnerFields = await db.FieldDefinitions
            .Where(x => x.EntityTypeId == integrationPartnerTypeId && blockedPartnerKeys.Contains(x.Key))
            .ToListAsync(ct);

        foreach (var field in blockedPartnerFields)
        {
            if (field.IsActive)
            {
                field.IsActive = false;
                field.IsRequired = false;
                updated = true;
            }
        }

        var normalizedSystemFields = new List<(Guid EntityTypeId, string Key, string DataType, string? OptionsJson)>
        {
            (userTypeId, "userId", "system", BuildOptionsJson(null, "uid", null, null, null, true)),
            (employeeTypeId, "hireDate", "datetime", BuildOptionsJson(null, "date"))
        };

        foreach (var target in normalizedSystemFields)
        {
            var field = await db.FieldDefinitions.FirstOrDefaultAsync(
                x => x.EntityTypeId == target.EntityTypeId && x.Key == target.Key,
                ct);

            if (field == null)
                continue;

            if (!string.Equals(field.DataType, target.DataType, StringComparison.OrdinalIgnoreCase))
            {
                field.DataType = target.DataType;
                updated = true;
            }

            var optionsResult = MetaFieldOptionsValidator.Validate(target.OptionsJson, target.DataType);
            if (!optionsResult.IsValid)
            {
                var summary = string.Join(", ", optionsResult.Errors.Select(x => x.Code));
                throw new InvalidOperationException($"Invalid OptionsJson for field '{target.Key}': {summary}");
            }

            var normalizedOptions = optionsResult.NormalizedJson;

            if (!string.Equals(field.OptionsJson, normalizedOptions, StringComparison.OrdinalIgnoreCase))
            {
                field.OptionsJson = normalizedOptions;
                updated = true;
            }
        }

        if (updated)
        {
        await db.SaveChangesAsync(ct);

        }

        _logger.LogInformation("Seeded system meta fields");
    }

    private static MetaFieldDefinition SystemField(
        Guid entityTypeId,
        string key,
        string title,
        string dataType,
        int order,
        bool required,
        string? referenceSourceJson = null,
        string? optionsJson = null,
        bool isSystem = true)
    {
        return new MetaFieldDefinition
        {
            Id = Guid.NewGuid(),
            EntityTypeId = entityTypeId,
            Key = key,
            Title = title,
            DataType = dataType,
            Order = order,
            IsRequired = required,
            IsSystem = isSystem,
            IsActive = true,
            OptionsJson = optionsJson,
            ValidationJson = null,
            ReferenceSourceJson = referenceSourceJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ReferenceList(string listKey) =>
        $"{{\"type\":\"list\",\"listKey\":\"{listKey}\"}}";

    private static string ReferenceEntity(string entityTypeKey) =>
        $"{{\"type\":\"entity\",\"entityTypeKey\":\"{entityTypeKey}\"}}";

    private static string? BuildOptionsJson(
        string? description,
        string? uiPreset = null,
        object? behavior = null,
        object? constraints = null,
        string? category = null,
        bool? advanced = null)
    {
        var payload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(description))
            payload["description"] = description;
        if (!string.IsNullOrWhiteSpace(uiPreset))
            payload["uiPreset"] = uiPreset;
        if (behavior != null)
            payload["behavior"] = behavior;
        if (constraints != null)
            payload["constraints"] = constraints;
        if (!string.IsNullOrWhiteSpace(category))
            payload["category"] = category;
        if (advanced.HasValue)
            payload["advanced"] = advanced.Value;

        if (payload.Count == 0)
            return null;

        return JsonSerializer.Serialize(payload);
    }

    private async Task SeedFieldTypesAsync(MetaDbContext db, CancellationToken ct)
    {
        var types = new List<(string Key, string Title, string Description)>
        {
            ("text", "Text", "String value"),
            ("number", "Number", "Integer or decimal"),
            ("boolean", "Boolean", "True/false flag"),
            ("datetime", "Date/Time", "Date and time"),
            ("select", "Select", "Selection from source"),
            ("reference", "Reference", "Entity reference"),
            ("file", "File", "File or media"),
            ("json", "JSON", "Arbitrary structure"),
            ("system", "System", "System fields")
        };

        foreach (var (key, title, description) in types)
        {
            var exists = await db.FieldTypes.AnyAsync(x => x.Key == key, ct);
            if (exists)
                continue;

            db.FieldTypes.Add(new MetaFieldType
            {
                Id = Guid.NewGuid(),
                Key = key,
                Title = title,
                Description = description,
                UiSchemaJson = null,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var coreKeys = new HashSet<string>(types.Select(x => x.Key));
        var legacyTypes = await db.FieldTypes.Where(x => x.IsSystem && !coreKeys.Contains(x.Key)).ToListAsync(ct);
        if (legacyTypes.Count > 0)
        {
            var inUse = await db.FieldDefinitions
                .Where(x => legacyTypes.Select(t => t.Key).Contains(x.DataType))
                .Select(x => x.DataType)
                .Distinct()
                .ToListAsync(ct);

            foreach (var legacy in legacyTypes)
            {
                if (!inUse.Contains(legacy.Key))
                {
                    legacy.IsActive = false;
                }
            }
        }
    }

    private async Task SeedListsAsync(MetaDbContext db, CancellationToken ct)
    {
        var lists = new List<(string Key, string Title, string Description, string SyncMode, bool IsSystem)>
        {
            ("organizations", "Organizations", "Organization registry list.", "external", true),
            ("departments", "Departments", "Departments registry list.", "external", true),
            ("positions", "Positions", "Positions registry list.", "external", true),
            ("node_templates", "Node templates", "Graph node template registry", "external", true),
            ("scan_fields", "Scan Fields", "Catalog of fields available for partner /scan.", "manual", true),
            ("profile_tabs", "Profile Tabs", "System profile tabs.", "manual", true),
            ("loyalty_levels", "Loyalty Levels", "Loyalty levels list (title + custom fields).", "manual", true),
            ("motivational_tiers", "Motivational Tiers", "Monthly motivational discount tiers based on earned points.", "manual", true),
            ("reward_types", "Типы наград", "Справочник типов наград для лидерборда.", "manual", true),
            ("leaderboard_rewards_monthly", "Награды лидерборда (месяц)", "Награды за места в ежемесячном лидерборде.", "manual", true),
            ("leaderboard_rewards_yearly", "Награды лидерборда (год)", "Награды за места в годовом лидерборде.", "manual", true),
        };

        foreach (var (key, title, description, syncMode, isSystem) in lists)
        {
            var existing = await db.Lists.FirstOrDefaultAsync(x => x.Key == key, ct);
            if (existing == null)
            {
                db.Lists.Add(new MetaList
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Title = title,
                    Description = description,
                    SyncMode = syncMode,
                    IsSystem = isSystem,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            if (existing.IsSystem != isSystem || existing.Title != title || existing.Description != description || existing.SyncMode != syncMode)
            {
                existing.Title = title;
                existing.Description = description;
                existing.SyncMode = syncMode;
                existing.IsSystem = isSystem;
                existing.IsActive = true;
            }
        }

        var legacySections = await db.Lists.FirstOrDefaultAsync(x => x.Key == "profile_sections", ct);
        if (legacySections != null)
        {
            legacySections.IsActive = false;
            legacySections.IsSystem = true;
        }


        var allLists = await db.Lists.ToListAsync(ct);
        foreach (var list in allLists)
        {
            await EnsureListEntityTypeAsync(db, list, ct);
        }

        await db.SaveChangesAsync(ct);

    }

    
    private static async Task EnsureListEntityTypeAsync(MetaDbContext db, MetaList list, CancellationToken ct)
    {
        var entityKey = $"list:{list.Key}";
        var entity = list.EntityTypeId.HasValue
            ? await db.EntityTypes.FirstOrDefaultAsync(x => x.Id == list.EntityTypeId.Value, ct)
            : await db.EntityTypes.FirstOrDefaultAsync(x => x.Key == entityKey, ct);

        if (entity == null)
        {
            entity = new MetaEntityType
            {
                Id = Guid.NewGuid(),
                Key = entityKey,
                Title = list.Title,
                Description = list.Description,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.EntityTypes.Add(entity);
        }
        else
        {
            if (!string.Equals(entity.Key, entityKey, StringComparison.OrdinalIgnoreCase))
                entity.Key = entityKey;
            entity.Title = list.Title;
            entity.Description = list.Description;
            entity.IsActive = list.IsActive;
        }

        list.EntityTypeId = entity.Id;

        var nameField = await db.FieldDefinitions.FirstOrDefaultAsync(x => x.EntityTypeId == entity.Id && x.Key == "name", ct);
        if (nameField == null)
        {
            db.FieldDefinitions.Add(new MetaFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityTypeId = entity.Id,
                Key = "name",
                Title = "Name",
                DataType = "text",
                Order = 0,
                IsRequired = true,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var legacyTitle = await db.FieldDefinitions.FirstOrDefaultAsync(x => x.EntityTypeId == entity.Id && x.Key == "title", ct);
        if (legacyTitle != null && legacyTitle.IsSystem)
        {
            legacyTitle.IsActive = false;
            legacyTitle.IsRequired = false;
        }
    }

    private async Task SeedSystemListFieldsAsync(MetaDbContext db, CancellationToken ct)
    {
        var loyaltyList = await db.Lists.FirstOrDefaultAsync(x => x.Key == "loyalty_levels", ct);
        if (loyaltyList == null)
            return;

        if (!loyaltyList.EntityTypeId.HasValue)
        {
            await EnsureListEntityTypeAsync(db, loyaltyList, ct);
            await db.SaveChangesAsync(ct);
        }

        if (!loyaltyList.EntityTypeId.HasValue)
            return;

        var entityId = loyaltyList.EntityTypeId.Value;
        var discountField = await db.FieldDefinitions.FirstOrDefaultAsync(
            x => x.EntityTypeId == entityId && x.Key == "discount",
            ct);
        var yearsField = await db.FieldDefinitions.FirstOrDefaultAsync(
            x => x.EntityTypeId == entityId && x.Key == "years",
            ct);

        if (discountField == null)
        {
            db.FieldDefinitions.Add(new MetaFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityTypeId = entityId,
                Key = "discount",
                Title = "\u0421\u043a\u0438\u0434\u043a\u0430",
                DataType = "number",
                Order = 1,
                IsRequired = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            discountField.Title = "\u0421\u043a\u0438\u0434\u043a\u0430";
            discountField.DataType = "number";
            discountField.IsActive = true;
        }

        if (yearsField == null)
        {
            db.FieldDefinitions.Add(new MetaFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityTypeId = entityId,
                Key = "years",
                Title = "\u0421\u0442\u0430\u0436",
                DataType = "number",
                Order = 2,
                IsRequired = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            yearsField.Title = "\u0421\u0442\u0430\u0436";
            yearsField.DataType = "number";
            yearsField.IsActive = true;
        }

        var basePayload = JsonSerializer.Serialize(new
        {
            name = "Base",
            discount = 0,
            years = 0
        }, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var baseItem = await db.ListItems.FirstOrDefaultAsync(
            x => x.ListId == loyaltyList.Id && x.Code == "base",
            ct);

        if (baseItem == null)
        {
            db.ListItems.Add(new MetaListItem
            {
                Id = Guid.NewGuid(),
                ListId = loyaltyList.Id,
                Code = "base",
                Title = "Base",
                ValueJson = basePayload,
                ExternalId = "base",
                IsActive = true,
                Order = 0,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            baseItem.Title = "Base";
            baseItem.ValueJson = basePayload;
            baseItem.ExternalId = baseItem.ExternalId ?? "base";
            baseItem.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        // Also seed motivational_tiers
        await SeedMotivationalTiersAsync(db, ct);
    }

    private async Task SeedMotivationalTiersAsync(MetaDbContext db, CancellationToken ct)
    {
        var tiersList = await db.Lists.FirstOrDefaultAsync(x => x.Key == "motivational_tiers", ct);
        if (tiersList == null)
            return;

        if (!tiersList.EntityTypeId.HasValue)
        {
            await EnsureListEntityTypeAsync(db, tiersList, ct);
            await db.SaveChangesAsync(ct);
        }

        if (!tiersList.EntityTypeId.HasValue)
            return;

        var entityId = tiersList.EntityTypeId.Value;

        // Deactivate min_points field - no longer needed, minPoints is now in Title
        var minPointsField = await db.FieldDefinitions.FirstOrDefaultAsync(
            x => x.EntityTypeId == entityId && x.Key == "min_points",
            ct);

        if (minPointsField != null)
        {
            minPointsField.IsActive = false;
            minPointsField.IsRequired = false;
        }

        // Add discount field (the only custom field now)
        var discountField = await db.FieldDefinitions.FirstOrDefaultAsync(
            x => x.EntityTypeId == entityId && x.Key == "discount",
            ct);

        if (discountField == null)
        {
            db.FieldDefinitions.Add(new MetaFieldDefinition
            {
                Id = Guid.NewGuid(),
                EntityTypeId = entityId,
                Key = "discount",
                Title = "Скидка (%)",
                DataType = "number",
                Order = 1,
                IsRequired = true,
                IsSystem = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            discountField.Title = "Скидка (%)";
            discountField.DataType = "number";
            discountField.Order = 1;
            discountField.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        // NOTE: Default tier items are NOT seeded.
        // User should create tiers via the UI (/admin/lists).
        // This prevents seeder from overwriting user changes.

        // Also seed leaderboard rewards
        await SeedLeaderboardRewardsAsync(db, ct);
    }

    private async Task SeedLeaderboardRewardsAsync(MetaDbContext db, CancellationToken ct)
    {
        var listKeys = new[] { "leaderboard_rewards_monthly", "leaderboard_rewards_yearly" };

        foreach (var listKey in listKeys)
        {
            var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == listKey, ct);
            if (list == null)
                continue;

            if (!list.EntityTypeId.HasValue)
            {
                await EnsureListEntityTypeAsync(db, list, ct);
                await db.SaveChangesAsync(ct);
            }

            if (!list.EntityTypeId.HasValue)
                continue;

            var entityId = list.EntityTypeId.Value;

            // reward_type field as reference to reward_types list
            var rewardTypeField = await db.FieldDefinitions.FirstOrDefaultAsync(
                x => x.EntityTypeId == entityId && x.Key == "reward_type", ct);

            if (rewardTypeField == null)
            {
                db.FieldDefinitions.Add(new MetaFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    EntityTypeId = entityId,
                    Key = "reward_type",
                    Title = "Тип награды",
                    DataType = "reference",
                    Order = 1,
                    IsRequired = true,
                    IsSystem = true,
                    IsActive = true,
                    ReferenceSourceJson = ReferenceList("reward_types"),
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                rewardTypeField.Title = "Тип награды";
                rewardTypeField.DataType = "reference";
                rewardTypeField.ReferenceSourceJson = ReferenceList("reward_types");
                rewardTypeField.OptionsJson = null;
                rewardTypeField.Order = 1;
                rewardTypeField.IsActive = true;
            }

            // value field: amount or description
            var valueField = await db.FieldDefinitions.FirstOrDefaultAsync(
                x => x.EntityTypeId == entityId && x.Key == "value", ct);

            if (valueField == null)
            {
                db.FieldDefinitions.Add(new MetaFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    EntityTypeId = entityId,
                    Key = "value",
                    Title = "Значение",
                    DataType = "text",
                    Order = 2,
                    IsRequired = true,
                    IsSystem = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                valueField.Title = "Значение";
                valueField.DataType = "text";
                valueField.Order = 2;
                valueField.IsActive = true;
            }
        }

        await db.SaveChangesAsync(ct);

        // Seed the reward_types list with default items
        await SeedRewardTypesAsync(db, ct);
    }

    private async Task SeedRewardTypesAsync(MetaDbContext db, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == "reward_types", ct);
        if (list == null)
            return;

        if (!list.EntityTypeId.HasValue)
        {
            await EnsureListEntityTypeAsync(db, list, ct);
            await db.SaveChangesAsync(ct);
        }

        if (!list.EntityTypeId.HasValue)
            return;

        // Default reward types
        var defaultTypes = new List<(string Code, string Title)>
        {
            ("discount", "Скидка (%)"),
            ("points", "Баллы"),
            ("prize", "Приз"),
        };

        foreach (var (code, title) in defaultTypes)
        {
            var existing = await db.ListItems.FirstOrDefaultAsync(
                x => x.ListId == list.Id && x.Code == code, ct);

            if (existing == null)
            {
                db.ListItems.Add(new MetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = list.Id,
                    Code = code,
                    Title = title,
                    ValueJson = JsonSerializer.Serialize(new { name = title }),
                    ExternalId = code,
                    IsActive = true,
                    Order = defaultTypes.FindIndex(x => x.Code == code),
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Update title but don't overwrite user changes
                if (!existing.IsActive)
                    existing.IsActive = true;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedNodeTemplatesAsync(MetaDbContext db, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == "node_templates", ct);
        if (list == null)
        {
            return;
        }

          var templates = new List<(string Code, string Title, object Value)>
        {
            ("start", "\u0421\u0442\u0430\u0440\u0442", new
            {
                category = "flow",
                description = "\u0422\u043e\u0447\u043a\u0430 \u0432\u0445\u043e\u0434\u0430 \u0441\u0446\u0435\u043d\u0430\u0440\u0438\u044f.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string>(),
                flow = new { allowNext = true, allowBody = false },
                settings = new { schema = new { fields = Array.Empty<object>() } },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("end", "\u041a\u043e\u043d\u0435\u0446", new
            {
                category = "flow",
                description = "\u0422\u043e\u0447\u043a\u0430 \u0437\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0438\u044f \u0441\u0446\u0435\u043d\u0430\u0440\u0438\u044f.",
                outputs = Array.Empty<string>(),
                requiredProps = new Dictionary<string, string>(),
                flow = new { allowNext = false, allowBody = false },
                settings = new { schema = new { fields = Array.Empty<object>() } },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("trigger", "\u0421\u0442\u0430\u0440\u0442 \u043f\u043e \u0441\u043e\u0431\u044b\u0442\u0438\u044e", new
            {
                category = "trigger",
                description = "\u0421\u0442\u0430\u0440\u0442 \u0441\u0446\u0435\u043d\u0430\u0440\u0438\u044f \u043f\u0440\u0438 \u043f\u043e\u0441\u0442\u0443\u043f\u043b\u0435\u043d\u0438\u0438 \u0441\u043e\u0431\u044b\u0442\u0438\u044f.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string>(),
                flow = new { allowNext = true, allowBody = false },
                settings = new { schema = new { fields = Array.Empty<object>() } },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("filter", "\u0423\u0441\u043b\u043e\u0432\u0438\u0435 (\u043f\u0440\u043e\u0441\u0442\u043e\u0435)", new
            {
                category = "condition",
                description = "\u041f\u0440\u043e\u0441\u0442\u043e\u0435 \u0441\u0440\u0430\u0432\u043d\u0435\u043d\u0438\u0435 \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0439.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string> { ["field"] = "string", ["operator"] = "string", ["value"] = "string" },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "field", type = "string", label = "\u041f\u043e\u043b\u0435", required = true, placeholder = "\u043d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: employee.hireDate" },
                            new { key = "operator", type = "select", label = "\u041e\u043f\u0435\u0440\u0430\u0442\u043e\u0440", required = true, options = new[]
                                {
                                    new { label = "\u0420\u0430\u0432\u043d\u043e", value = "eq" },
                                    new { label = "\u041d\u0435 \u0440\u0430\u0432\u043d\u043e", value = "neq" },
                                    new { label = "\u0411\u043e\u043b\u044c\u0448\u0435", value = "gt" },
                                    new { label = "\u0411\u043e\u043b\u044c\u0448\u0435 \u0438\u043b\u0438 \u0440\u0430\u0432\u043d\u043e", value = "gte" },
                                    new { label = "\u041c\u0435\u043d\u044c\u0448\u0435", value = "lt" },
                                    new { label = "\u041c\u0435\u043d\u044c\u0448\u0435 \u0438\u043b\u0438 \u0440\u0430\u0432\u043d\u043e", value = "lte" },
                                    new { label = "\u0421\u043e\u0434\u0435\u0440\u0436\u0438\u0442", value = "contains" }
                                }
                            },
                            new { key = "value", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u0435", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("counter", "\u0421\u0447\u0435\u0442\u0447\u0438\u043a", new
            {
                category = "condition",
                description = "\u041f\u0440\u043e\u043f\u0443\u0441\u043a\u0430\u0435\u0442 \u043f\u043e\u0441\u043b\u0435 N \u0441\u0440\u0430\u0431\u0430\u0442\u044b\u0432\u0430\u043d\u0438\u0439.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string> { ["target"] = "int" },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "target", type = "number", label = "\u0421\u043a\u043e\u043b\u044c\u043a\u043e \u0440\u0430\u0437", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("cooldown", "\u041f\u0430\u0443\u0437\u0430", new
            {
                category = "condition",
                description = "\u041d\u0435 \u043f\u0440\u043e\u043f\u0443\u0441\u043a\u0430\u0435\u0442 \u043f\u043e\u043a\u0430 \u043d\u0435 \u043f\u0440\u043e\u0439\u0434\u0435\u0442 \u0442\u0430\u0439\u043c\u0430\u0443\u0442.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string> { ["seconds"] = "int" },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "seconds", type = "number", label = "\u0421\u0435\u043a\u0443\u043d\u0434\u044b \u043f\u0430\u0443\u0437\u044b", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("condition", "\u0423\u0441\u043b\u043e\u0432\u0438\u0435", new
            {
                category = "condition",
                description = "\u0423\u043d\u0438\u0432\u0435\u0440\u0441\u0430\u043b\u044c\u043d\u043e\u0435 \u0443\u0441\u043b\u043e\u0432\u0438\u0435 (\u0440\u0430\u0432\u043d\u043e / \u0434\u0438\u0430\u043f\u0430\u0437\u043e\u043d / \u0441\u043f\u0438\u0441\u043e\u043a).",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string>
                {
                    ["mode"] = "string",
                    ["source"] = "string"
                },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                branchSettings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new
                            {
                                key = "mode",
                                type = "select",
                                label = "\u0422\u0438\u043f \u0443\u0441\u043b\u043e\u0432\u0438\u044f",
                                required = true,
                                options = new object[]
                                {
                                    new { value = "equals", label = "\u0420\u0430\u0432\u043d\u043e" },
                                    new { value = "range", label = "\u0414\u0438\u0430\u043f\u0430\u0437\u043e\u043d" },
                                    new { value = "contains", label = "\u0421\u043f\u0438\u0441\u043e\u043a" }
                                }
                            },
                            new
                            {
                                key = "source",
                                type = "select",
                                label = "\u041f\u043e\u043b\u0435",
                                required = true,
                                optionsSource = new
                                {
                                    type = "meta_fields",
                                    entityTypeKeys = new[] { "user", "employee" }
                                }
                            },
                            new { key = "value", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u0435", required = false },
                            new { key = "values", type = "string", label = "\u0421\u043f\u0438\u0441\u043e\u043a (\u0447\u0435\u0440\u0435\u0437 \u0437\u0430\u043f\u044f\u0442\u0443\u044e)", required = false },
                            new { key = "min", type = "number", label = "\u041c\u0438\u043d\u0438\u043c\u0443\u043c", required = false },
                            new { key = "max", type = "number", label = "\u041c\u0430\u043a\u0441\u0438\u043c\u0443\u043c", required = false },
                            new { key = "inclusive", type = "boolean", label = "\u0412\u043a\u043b\u044e\u0447\u0430\u044f \u0433\u0440\u0430\u043d\u0438\u0446\u044b", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 1,
                deprecated = false
            }),
            ("range_switch", "\u041f\u0440\u043e\u0432\u0435\u0440\u043a\u0430 \u0434\u0438\u0430\u043f\u0430\u0437\u043e\u043d\u0430", new
            {
                category = "condition",
                description = "\u041f\u0440\u043e\u0432\u0435\u0440\u044f\u0435\u0442, \u0447\u0442\u043e \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u0432 \u0434\u0438\u0430\u043f\u0430\u0437\u043e\u043d\u0435.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string>
                {
                    ["source"] = "string",
                    ["min"] = "number",
                    ["max"] = "number?"
                },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "source", type = "string", label = "\u0418\u0441\u0442\u043e\u0447\u043d\u0438\u043a", required = true, placeholder = "\u043d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: employee.tenureYears" },
                            new { key = "min", type = "number", label = "\u041c\u0438\u043d\u0438\u043c\u0443\u043c", required = true },
                            new { key = "max", type = "number", label = "\u041c\u0430\u043a\u0441\u0438\u043c\u0443\u043c", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 3,
                deprecated = true
            }),
            ("equals_switch", "\u0420\u0430\u0432\u043d\u043e", new
            {
                category = "condition",
                description = "\u0421\u0440\u0430\u0432\u043d\u0438\u0432\u0430\u0435\u0442 \u0434\u0432\u0430 \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u044f.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string>
                {
                    ["source"] = "string",
                    ["value"] = "string"
                },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "source", type = "string", label = "\u0418\u0441\u0442\u043e\u0447\u043d\u0438\u043a", required = true, placeholder = "\u043d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: user.level" },
                            new { key = "value", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u0435", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 3,
                deprecated = true
            }),
            ("contains_switch", "\u0421\u043e\u0434\u0435\u0440\u0436\u0438\u0442", new
            {
                category = "condition",
                description = "\u041f\u0440\u043e\u0432\u0435\u0440\u044f\u0435\u0442, \u0435\u0441\u0442\u044c \u043b\u0438 \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u0432 \u0441\u043f\u0438\u0441\u043a\u0435.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string>
                {
                    ["source"] = "string",
                    ["values"] = "string"
                },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "source", type = "string", label = "\u0418\u0441\u0442\u043e\u0447\u043d\u0438\u043a", required = true, placeholder = "\u043d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: user.tags" },
                            new { key = "values", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u044f (\u0447\u0435\u0440\u0435\u0437 \u0437\u0430\u043f\u044f\u0442\u0443\u044e)", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 3,
                deprecated = true
            }),
            ("compute_tenure", "\u0421\u0442\u0430\u0436 (\u0432 \u0433\u043e\u0434\u0430\u0445)", new
            {
                category = "data",
                description = "\u0421\u0447\u0438\u0442\u0430\u0435\u0442 \u0441\u0442\u0430\u0436 \u043f\u043e \u0434\u0430\u0442\u0435.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["source"] = "string", ["target"] = "string?" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "source", type = "string", label = "\u0414\u0430\u0442\u0430 (\u043f\u043e\u043b\u0435)", required = true, placeholder = "\u043d\u0430\u043f\u0440\u0438\u043c\u0435\u0440: employee.hireDate" },
                            new { key = "target", type = "string", label = "\u041a\u0443\u0434\u0430 \u0441\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c", required = false, placeholder = "tenureYears" }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("streak_daily", "\u0415\u0436\u0435\u0434\u043d\u0435\u0432\u043d\u044b\u0439 \u0431\u043e\u043d\u0443\u0441", new
            {
                category = "condition",
                description = "\u041d\u0430\u0447\u0438\u0441\u043b\u044f\u0435\u0442 \u0431\u043e\u043d\u0443\u0441 \u0437\u0430 \u0441\u0435\u0440\u0438\u044e \u0434\u043d\u0435\u0439.",
                outputs = new[] { "true", "false" },
                requiredProps = new Dictionary<string, string>
                {
                    ["basePoints"] = "number",
                    ["stepPoints"] = "number?",
                    ["maxPoints"] = "number?"
                },
                flow = new
                {
                    branches = new[] { new { id = "true", label = "\u0414\u0430" }, new { id = "false", label = "\u041d\u0435\u0442" } },
                    allowNext = false,
                    allowBody = false
                },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "basePoints", type = "number", label = "\u0411\u0430\u0437\u043e\u0432\u044b\u0435 \u0431\u0430\u043b\u043b\u044b", required = true },
                            new { key = "stepPoints", type = "number", label = "\u0414\u043e\u0431\u0430\u0432\u043a\u0430 \u0437\u0430 \u0434\u0435\u043d\u044c", required = false },
                            new { key = "maxPoints", type = "number", label = "\u041c\u0430\u043a\u0441\u0438\u043c\u0443\u043c", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 2,
                deprecated = false
            }),
            ("var_set", "\u0423\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c \u043f\u0435\u0440\u0435\u043c\u0435\u043d\u043d\u0443\u044e", new
            {
                category = "data",
                description = "\u0421\u043e\u0445\u0440\u0430\u043d\u044f\u0435\u0442 \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u0432 \u043f\u0435\u0440\u0435\u043c\u0435\u043d\u043d\u0443\u044e.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["key"] = "string", ["value"] = "string?" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "key", type = "string", label = "\u041a\u043b\u044e\u0447", required = true },
                            new { key = "value", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u0435", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("state_get", "\u0412\u0437\u044f\u0442\u044c \u0441\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0435", new
            {
                category = "data",
                description = "\u0411\u0435\u0440\u0435\u0442 \u0438\u0437 \u0445\u0440\u0430\u043d\u0438\u043b\u0438\u0449\u0430 \u0441\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0435 \u0438 \u043a\u043b\u0430\u0434\u0435\u0442 \u0432 \u043f\u0435\u0440\u0435\u043c\u0435\u043d\u043d\u0443\u044e.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["key"] = "string", ["target"] = "string" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "key", type = "string", label = "\u041a\u043b\u044e\u0447 \u0432 \u0441\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0438", required = true },
                            new { key = "target", type = "string", label = "\u041a\u0443\u0434\u0430 \u0441\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("state_set", "\u0417\u0430\u043f\u0438\u0441\u0430\u0442\u044c \u0441\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0435", new
            {
                category = "data",
                description = "\u041f\u0438\u0448\u0435\u0442 \u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u0432 \u0441\u043e\u0441\u0442\u043e\u044f\u043d\u0438\u0435 \u043d\u043e\u0434\u044b.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["key"] = "string", ["value"] = "string?" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "key", type = "string", label = "\u041a\u043b\u044e\u0447", required = true },
                            new { key = "value", type = "string", label = "\u0417\u043d\u0430\u0447\u0435\u043d\u0438\u0435", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "all" },
                version = 2,
                deprecated = false
            }),
            ("audience_selector", "\u0410\u0443\u0434\u0438\u0442\u043e\u0440\u0438\u044f", new
            {
                category = "audience",
                description = "\u0412\u044b\u0431\u043e\u0440 \u0430\u0443\u0434\u0438\u0442\u043e\u0440\u0438\u0438 \u0434\u043b\u044f \u043c\u0430\u0441\u0441\u043e\u0432\u043e\u0433\u043e \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f. \u041f\u043e \u0443\u043c\u043e\u043b\u0447\u0430\u043d\u0438\u044e \u0432\u0441\u0435 \u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u0438, \u043a\u0440\u043e\u043c\u0435 isRoot.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string>(),
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "entity", type = "select", label = "\u0421\u0443\u0449\u043d\u043e\u0441\u0442\u044c", required = false, options = new[]
                                {
                                    new { label = "\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u0438", value = "user" },
                                    new { label = "\u0421\u043e\u0442\u0440\u0443\u0434\u043d\u0438\u043a\u0438", value = "employee" }
                                }
                            },
                            new { key = "limit", type = "number", label = "\u041e\u0433\u0440\u0430\u043d\u0438\u0447\u0435\u043d\u0438\u0435", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 3,
                deprecated = false
            }),
            ("award", "\u041d\u0430\u0447\u0438\u0441\u043b\u0438\u0442\u044c \u0431\u043e\u043d\u0443\u0441\u044b", new
            {
                category = "action",
                description = "\u0414\u043e\u0431\u0430\u0432\u043b\u044f\u0435\u0442 \u0431\u0430\u043b\u043b\u044b \u043d\u0430 \u0441\u0447\u0435\u0442.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["points"] = "number" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "points", type = "number", label = "\u0411\u0430\u043b\u043b\u044b", required = true }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 2,
                deprecated = false
            }),
            ("action_update_profile", "\u041e\u0431\u043d\u043e\u0432\u0438\u0442\u044c \u043f\u0440\u043e\u0444\u0438\u043b\u044c", new
            {
                category = "action",
                description = "\u041c\u0435\u043d\u044f\u0435\u0442 \u0443\u0440\u043e\u0432\u0435\u043d\u044c \u0438 \u0442\u0435\u0433\u0438.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string>
                {
                    ["setLevel"] = "string?",
                    ["addTags"] = "string?"
                },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new
                            {
                                key = "setLevel",
                                type = "select",
                                label = "\u0423\u0440\u043e\u0432\u0435\u043d\u044c",
                                required = false,
                                optionsSource = new
                                {
                                    type = "meta_list_items",
                                    listKey = "loyalty_levels"
                                }
                            },
                            new { key = "addTags", type = "string", label = "\u0422\u0435\u0433\u0438 (\u0447\u0435\u0440\u0435\u0437 \u0437\u0430\u043f\u044f\u0442\u0443\u044e)", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 2,
                deprecated = false
            }),
            ("action_notification", "\u0423\u0432\u0435\u0434\u043e\u043c\u043b\u0435\u043d\u0438\u0435", new
            {
                category = "action",
                description = "\u041e\u0442\u043f\u0440\u0430\u0432\u043b\u044f\u0435\u0442 \u0443\u0432\u0435\u0434\u043e\u043c\u043b\u0435\u043d\u0438\u0435 \u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044e.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["channel"] = "string" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "channel", type = "select", label = "\u041a\u0430\u043d\u0430\u043b", required = true, options = new[]
                                {
                                    new { label = "Push", value = "push" },
                                    new { label = "Email", value = "email" },
                                    new { label = "SMS", value = "sms" },
                                    new { label = "\u0412 \u043f\u0440\u0438\u043b\u043e\u0436\u0435\u043d\u0438\u0438", value = "inapp" }
                                }
                            },
                            new { key = "message", type = "string", label = "\u0422\u0435\u043a\u0441\u0442", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 2,
                deprecated = false
            }),
            ("action_feed_post", "\u041f\u043e\u0441\u0442 \u0432 \u043b\u0435\u043d\u0442\u0443", new
            {
                category = "action",
                description = "\u041f\u0443\u0431\u043b\u0438\u043a\u0443\u0435\u0442 \u0441\u043e\u043e\u0431\u0449\u0435\u043d\u0438\u0435 \u0432 \u043b\u0435\u043d\u0442\u0443.",
                outputs = new[] { "next" },
                requiredProps = new Dictionary<string, string> { ["channel"] = "string" },
                flow = new { allowNext = true, allowBody = false },
                settings = new
                {
                    schema = new
                    {
                        fields = new object[]
                        {
                            new { key = "channel", type = "string", label = "\u041a\u0430\u043d\u0430\u043b", required = true },
                            new { key = "message", type = "string", label = "\u0422\u0435\u043a\u0441\u0442", required = false }
                        }
                    }
                },
                advanced = false,
                contexts = new[] { "loyalty" },
                version = 2,
                deprecated = false
            })
        };

        foreach (var (code, title, value) in templates)
        {
            var existing = await db.ListItems.FirstOrDefaultAsync(
                x => x.ListId == list.Id && x.Code == code,
                ct);

            var json = JsonSerializer.Serialize(value);
            if (existing == null)
            {
                db.ListItems.Add(new MetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = list.Id,
                    Code = code,
                    Title = title,
                    ValueJson = json,
                    IsActive = true,
                    Order = 0,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = title;
                existing.ValueJson = json;
                existing.IsActive = true;
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded node templates list");
    }

    private async Task SeedScanFieldsAsync(MetaDbContext db, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == "scan_fields", ct);
        if (list == null)
            return;

        var seeds = new List<ScanFieldSeed>
        {
            new("user.firstName", "First name", "user", "string", "profile", new Dictionary<string, string> { ["path"] = "firstName" }, 10, true, false, true, null),
            new("user.lastName", "Last name", "user", "string", "profile", new Dictionary<string, string> { ["path"] = "lastName" }, 20, true, false, true, null),
            new("user.avatarUrl", "Avatar URL", "user", "string", "profile", new Dictionary<string, string> { ["path"] = "avatarUrl" }, 30, true, false, true, null),
            new("discountUser", "User discount", "loyalty", "number", "loyalty_profile", new Dictionary<string, string>
            {
                ["path"] = "totalDiscount"
            }, 100, true, false, true, null),
            new("discountPartner", "Partner discount", "partner", "number", "partner", new Dictionary<string, string>
            {
                ["path"] = "discountPartner"
            }, 110, true, false, true, null),
        };

        var allowedKeys = new HashSet<string>(seeds.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);
        var staleItems = await db.ListItems
            .Where(x => x.ListId == list.Id && !allowedKeys.Contains(x.Code))
            .ToListAsync(ct);
        if (staleItems.Count > 0)
            db.ListItems.RemoveRange(staleItems);

        foreach (var seed in seeds)
        {
            var existing = await db.ListItems.FirstOrDefaultAsync(
                x => x.ListId == list.Id && x.Code == seed.Key,
                ct);

            if (existing == null)
            {
                var payload = BuildScanFieldPayload(seed, null, null);
                db.ListItems.Add(new MetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = list.Id,
                    Code = seed.Key,
                    Title = seed.Title,
                    ValueJson = payload,
                    IsActive = seed.IsEnabled,
                    Order = seed.SortOrder,
                    CreatedAt = DateTime.UtcNow
                });
                continue;
            }

            var overlay = ReadScanFieldOverlay(existing.ValueJson);
            var payloadWithOverlay = BuildScanFieldPayload(seed, overlay, existing);

            existing.Title = overlay.Title ?? existing.Title ?? seed.Title;
            existing.ValueJson = payloadWithOverlay;
            existing.IsActive = overlay.IsEnabled ?? existing.IsActive;
            existing.Order = overlay.SortOrder ?? existing.Order;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded scan_fields list");
    }

    private static bool HasResolver(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty("resolver", out var resolver))
                return false;

            return resolver.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(resolver.GetString());
        }
        catch
        {
            return false;
        }
    }

    private static ScanFieldOverlay ReadScanFieldOverlay(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ScanFieldOverlay();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new ScanFieldOverlay();

            var root = doc.RootElement;
            return new ScanFieldOverlay(
                Title: ReadString(root, "title"),
                Group: ReadString(root, "group"),
                Description: ReadString(root, "description"),
                SortOrder: ReadInt(root, "sortOrder"),
                IsAdvanced: ReadBool(root, "isAdvanced"),
                IsEnabled: ReadBool(root, "isEnabled"),
                Expose: ReadBool(root, "expose"));
        }
        catch
        {
            return new ScanFieldOverlay();
        }
    }

    private static string BuildScanFieldPayload(ScanFieldSeed seed, ScanFieldOverlay? overlay, MetaListItem? existing)
    {
        var payload = new
        {
            title = overlay?.Title ?? existing?.Title ?? seed.Title,
            group = overlay?.Group ?? seed.Group,
            type = seed.Type,
            resolver = seed.Resolver,
            resolverConfig = seed.ResolverConfig,
            description = overlay?.Description ?? seed.Description,
            sortOrder = overlay?.SortOrder ?? seed.SortOrder,
            isAdvanced = overlay?.IsAdvanced ?? seed.IsAdvanced,
            isEnabled = overlay?.IsEnabled ?? existing?.IsActive ?? seed.IsEnabled,
            expose = overlay?.Expose ?? seed.Expose
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
            return result;
        return null;
    }

    private static bool? ReadBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private sealed record ScanFieldSeed(
        string Key,
        string Title,
        string Group,
        string Type,
        string Resolver,
        Dictionary<string, string> ResolverConfig,
        int SortOrder,
        bool IsEnabled,
        bool IsAdvanced,
        bool Expose,
        string? Description);

    private sealed record ScanFieldOverlay(
        string? Title = null,
        string? Group = null,
        string? Description = null,
        int? SortOrder = null,
        bool? IsAdvanced = null,
        bool? IsEnabled = null,
        bool? Expose = null);

    private async Task SeedProfileTabsAsync(MetaDbContext db, CancellationToken ct)
    {
        var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == "profile_tabs", ct);
        if (list == null)
            return;

        var items = new (string Code, string Title, int Order)[]
        {
            ("Account", "Account", 100),
            ("HRProfile", "HR profile", 200),
            ("LoyaltyProfile", "Loyalty profile", 300),
            ("Documents", "Documents", 400),
            ("Family", "Family", 500),
        };

        foreach (var (code, title, order) in items)
        {
            var existing = await db.ListItems.FirstOrDefaultAsync(x => x.ListId == list.Id && x.Code == code, ct);
            if (existing == null)
            {
                db.ListItems.Add(new MetaListItem
                {
                    Id = Guid.NewGuid(),
                    ListId = list.Id,
                    Code = code,
                    Title = title,
                    IsActive = true,
                    Order = order,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Title = title;
                existing.IsActive = true;
                existing.Order = order;
            }
        }

        var allowed = items.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = await db.ListItems.Where(x => x.ListId == list.Id && !allowed.Contains(x.Code)).ToListAsync(ct);
        foreach (var item in stale)
        {
            item.IsActive = false;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded profile_tabs list");
    }

}
