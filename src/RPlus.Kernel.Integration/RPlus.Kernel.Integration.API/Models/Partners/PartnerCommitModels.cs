using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPlus.Kernel.Integration.Api.Models.Partners;

/// <summary>
/// Request to commit an order (financial fact).
/// This is the most critical contract - it locks money.
/// </summary>
public class PartnerCommitRequest
{
    /// <summary>Reference to the scan intent.</summary>
    public Guid ScanId { get; set; }

    /// <summary>Order ID for anti-fraud verification.</summary>
    public Guid OrderId { get; set; }

    /// <summary>When order was closed on POS.</summary>
    public DateTime ClosedAt { get; set; }

    /// <summary>POS terminal identifier.</summary>
    public string? Terminal { get; set; }

    // ========== Financial Facts ==========

    /// <summary>Final order total (after discounts).</summary>
    public decimal FinalOrderTotal { get; set; }

    /// <summary>Order total before any discounts were applied.</summary>
    public decimal FinalOrderTotalBeforeDiscounts { get; set; }

    /// <summary>Final RPlus discount percent.</summary>
    public decimal FinalUserDiscount { get; set; }

    /// <summary>Final partner discount percent.</summary>
    public decimal FinalPartnerDiscount { get; set; }

    /// <summary>Actual RPlus discount amount in currency.</summary>
    public decimal? UserDiscountAmount { get; set; }

    /// <summary>Actual partner discount amount in currency.</summary>
    public decimal? PartnerDiscountAmount { get; set; }

    // ========== QR Discount Metadata ==========

    /// <summary>QR discount type ID (from plugin configuration).</summary>
    public string? QrDiscountTypeId { get; set; }

    /// <summary>QR discount percent that was applied.</summary>
    public decimal? QrDiscountPercent { get; set; }

    /// <summary>QR discount sum that was applied.</summary>
    public decimal? QrDiscountSum { get; set; }

    // ========== Receipt Metadata ==========

    /// <summary>Receipt information.</summary>
    public ChequeInfoDto? Cheque { get; set; }

    // ========== Analytics (JSONB) ==========

    /// <summary>Order items for analytics.</summary>
    public List<OrderItemDto>? Items { get; set; }

    /// <summary>Payment methods for analytics.</summary>
    public List<PaymentItemDto>? Payments { get; set; }

    /// <summary>Discount lines applied to the order.</summary>
    public List<DiscountItemDto>? Discounts { get; set; }

    /// <summary>Captures any unknown fields (forward-compat).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Receipt information.
/// </summary>
public class ChequeInfoDto
{
    /// <summary>Printed receipt number.</summary>
    public string? ChequeNumber { get; set; }

    /// <summary>Fiscal identifier (FD/FP).</summary>
    public string? FiscalId { get; set; }
}

/// <summary>
/// Order item for analytics.
/// </summary>
public class OrderItemDto
{
    /// <summary>External product ID.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Product name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unit price.</summary>
    public decimal Price { get; set; }

    /// <summary>Quantity.</summary>
    public decimal Amount { get; set; }

    /// <summary>Line total.</summary>
    public decimal Sum { get; set; }

    /// <summary>Product category.</summary>
    public string? Category { get; set; }
}

/// <summary>
/// Payment method for analytics.
/// </summary>
public class PaymentItemDto
{
    /// <summary>Payment type (cash, card, etc.).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Amount paid with this method.</summary>
    public decimal Amount { get; set; }

    /// <summary>Transaction reference (for card payments).</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Discount line applied to the order.
/// </summary>
public class DiscountItemDto
{
    /// <summary>Discount type ID.</summary>
    public string? DiscountTypeId { get; set; }

    /// <summary>Discount name.</summary>
    public string? Name { get; set; }

    /// <summary>Discount percent.</summary>
    public decimal? Percent { get; set; }

    /// <summary>Discount absolute amount.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Response for commit operation.
/// </summary>
public class PartnerCommitResponse
{
    /// <summary>Commit transaction ID.</summary>
    public Guid CommitId { get; set; }

    /// <summary>Whether this was a duplicate request (idempotent).</summary>
    public bool WasDuplicate { get; set; }
}
