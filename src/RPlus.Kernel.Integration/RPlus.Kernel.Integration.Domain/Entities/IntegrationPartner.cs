using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RPlus.SDK.Integration.Models;

#nullable enable
namespace RPlus.Kernel.Integration.Domain.Entities;

public class IntegrationPartner : RPlus.SDK.Integration.Models.IntegrationPartner
{
    public Dictionary<string, object> Metadata { get; set; } = new();
    private static readonly string[] DefaultDiscountProfileFields =
    {
        "user.firstName",
        "user.lastName",
        "user.avatarUrl",
        "discountUser",
        "discountPartner"
    };

    public static IReadOnlyList<string> DefaultDiscountProfileFieldKeys => DefaultDiscountProfileFields;

    private IntegrationPartner()
    {
    }

    public IntegrationPartner(string name, string? description, bool isDiscountPartner, decimal? discountPartner = null, string? accessLevel = null)
        : this(Guid.NewGuid(), name, description, isDiscountPartner, discountPartner, accessLevel)
    {
    }

    public IntegrationPartner(Guid id, string name, string? description, bool isDiscountPartner, decimal? discountPartner = null, string? accessLevel = null)
    {
        this.Id = id;
        this.Name = name;
        this.Description = description;
        this.IsDiscountPartner = isDiscountPartner;
        this.DiscountPartner = discountPartner;
        this.AccessLevel = NormalizeAccessLevel(accessLevel);
        this.IsActive = true;
        this.ProfileFields = isDiscountPartner
            ? new List<string>(DefaultDiscountProfileFields)
            : new List<string>();
        this.Metadata = new Dictionary<string, object>();
        this.CreatedAt = DateTime.UtcNow;
    }

    public void Update(string name, string? description, bool isDiscountPartner, decimal? discountPartner = null, string? accessLevel = null)
    {
        this.Name = name;
        this.Description = description;
        this.IsDiscountPartner = isDiscountPartner;
        this.DiscountPartner = discountPartner;
        this.AccessLevel = NormalizeAccessLevel(accessLevel ?? this.AccessLevel);
    }

    public void UpdateProfileFields(IEnumerable<string> fields)
    {
        this.ProfileFields = fields
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void UpdateMetadata(Dictionary<string, object> metadata)
    {
        this.Metadata = metadata ?? new Dictionary<string, object>();
    }

    public void Activate() => this.IsActive = true;

    public void Deactivate() => this.IsActive = false;

    public void Delete() => this.DeletedAt = DateTime.UtcNow;

    private static string NormalizeAccessLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "limited";

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "system" => "system",
            "limited" => "limited",
            _ => "limited"
        };
    }
}

