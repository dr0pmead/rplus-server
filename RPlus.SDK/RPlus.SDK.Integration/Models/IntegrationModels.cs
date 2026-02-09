using System;
using System.Collections.Generic;

#nullable enable

namespace RPlus.SDK.Integration.Models;

public class IntegrationPartner
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AccessLevel { get; set; } = "limited";
    public bool IsActive { get; set; }
    public bool IsDiscountPartner { get; set; }
    public decimal? DiscountPartner { get; set; }
    public List<string> ProfileFields { get; set; } = new();
    public Guid? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Dynamic Level-Based Discount System
    /// <summary>Discount calculation strategy: 'fixed' (legacy) or 'dynamic_level' (new)</summary>
    public string DiscountStrategy { get; set; } = "dynamic_level";
    /// <summary>Partner category: 'restaurant', 'services', 'retail'</summary>
    public string PartnerCategory { get; set; } = "retail";
    /// <summary>Custom max discount for this partner (overrides category default)</summary>
    public decimal? MaxDiscount { get; set; }
    /// <summary>Happy hours configuration JSON</summary>
    public string? HappyHoursConfigJson { get; set; }
}

public class IntegrationApiKey
{
    public Guid Id { get; set; }
    public Guid? PartnerId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string Environment { get; set; } = "production";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public List<string>? Scopes { get; set; }
}

public class IntegrationRoute
{
    public Guid Id { get; set; }
    public Guid? PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoutePattern { get; set; } = string.Empty;
    public string TargetHost { get; set; } = string.Empty;
    public string TargetService { get; set; } = string.Empty;
    public string TargetMethod { get; set; } = string.Empty;
    public string Transport { get; set; } = "grpc";
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class IntegrationStat
{
    public Guid Id { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public DateTime Timestamp { get; set; }
    public int RequestCount { get; set; }
    public int ErrorCount { get; set; }
    public double AvgLatencyMs { get; set; }
}

public class IntegrationAuditLog
{
    public Guid Id { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TraceId { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestPath { get; set; } = string.Empty;
    public string TargetService { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string ClientIp { get; set; } = string.Empty;
}
