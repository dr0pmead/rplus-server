using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPlus.Kernel.Integration.Api.Models.Partners;

/// <summary>
/// Request to register a scan intent (before order is closed).
/// Accepts either qrToken (JWT) or otpCode (short code).
/// Unknown fields are silently captured for forward-compat.
/// </summary>
public class PartnerScanRequest
{
    /// <summary>QR JWT token.</summary>
    public string? QrToken { get; set; }

    /// <summary>Short OTP code (e.g. "000000"). Plugin normalizes "000-000" → "000000".</summary>
    public string? OtpCode { get; set; }

    /// <summary>External order ID (e.g., iiko Order GUID).</summary>
    public Guid OrderId { get; set; }

    /// <summary>Current order total before discounts.</summary>
    public decimal OrderSum { get; set; }

    /// <summary>POS terminal identifier.</summary>
    public string? TerminalId { get; set; }

    /// <summary>Cashier who performed the scan (for audit).</summary>
    public string? CashierId { get; set; }

    /// <summary>Arbitrary context from the plugin (ignored by server, stored for audit).</summary>
    public JsonElement? Context { get; set; }

    /// <summary>Captures any unknown fields the plugin may send (forward-compat).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// Resolve the token input: prefers OtpCode if present, falls back to QrToken.
    /// </summary>
    [JsonIgnore]
    public string ResolvedToken =>
        !string.IsNullOrWhiteSpace(OtpCode)
            ? OtpCode.Replace("-", "").Trim()
            : QrToken ?? string.Empty;
}

/// <summary>
/// Response with scan intent result and predicted discounts.
/// Supports both old flat format and new grouped format.
/// </summary>
public class PartnerScanResponse
{
    /// <summary>Our transaction ID (for commit reference).</summary>
    public Guid ScanId { get; set; }

    /// <summary>When this scan intent expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>User profile data.</summary>
    public PartnerScanUserDto? User { get; set; }

    /// <summary>Predicted discount breakdown (new format, preferred).</summary>
    public PartnerScanDiscountsDto Discounts { get; set; } = new();

    // ─── Old Format Compat (flat fields) ─────────────────────────────────

    /// <summary>RPlus discount percent (old format compat).</summary>
    public decimal DiscountUser => Discounts.RPlusAmount;

    /// <summary>Partner discount percent (old format compat).</summary>
    public decimal DiscountPartner => Discounts.PartnerAmount;

    // ─── Optional Metadata ───────────────────────────────────────────────

    /// <summary>Warnings for the POS terminal (e.g., "loyalty level about to expire").</summary>
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// User profile data for scan response.
/// </summary>
public class PartnerScanUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public int CurrentLevel { get; set; }
    public int TotalLevels { get; set; }
}

/// <summary>
/// Predicted discount breakdown.
/// </summary>
public class PartnerScanDiscountsDto
{
    /// <summary>RPlus platform discount (compensation).</summary>
    public decimal RPlusAmount { get; set; }

    /// <summary>Partner marketing discount.</summary>
    public decimal PartnerAmount { get; set; }

    /// <summary>Total discount percent (informative).</summary>
    public decimal TotalPercent { get; set; }
}
