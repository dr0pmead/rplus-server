using System;

namespace RPlus.SDK.Contracts.Events;

/// <summary>
/// Published when a partner order is committed (closed with applied discounts).
/// Consumed by Wallet service to process cashback/loyalty points.
/// </summary>
public record PartnerOrderCommittedEvent
{
    /// <summary>
    /// Unique identifier of the commit record.
    /// </summary>
    public Guid CommitId { get; init; }

    /// <summary>
    /// Reference to the original scan intent.
    /// </summary>
    public Guid ScanId { get; init; }

    /// <summary>
    /// Partner who processed the order.
    /// </summary>
    public Guid PartnerId { get; init; }

    /// <summary>
    /// User who received the discount.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Final order total after discounts.
    /// </summary>
    public decimal OrderTotal { get; init; }

    /// <summary>
    /// RPlus platform discount amount (to be compensated from Wallet).
    /// </summary>
    public decimal UserDiscountAmount { get; init; }

    /// <summary>
    /// Partner's own discount amount.
    /// </summary>
    public decimal PartnerDiscountAmount { get; init; }

    /// <summary>
    /// When the order was committed.
    /// </summary>
    public DateTime CommittedAt { get; init; }

    /// <summary>
    /// Terminal ID where the order was processed.
    /// </summary>
    public string? TerminalId { get; init; }

    /// <summary>
    /// Fiscal receipt ID (if available).
    /// </summary>
    public string? FiscalId { get; init; }
}

/// <summary>
/// Published when a scan intent is registered.
/// Informational event for analytics.
/// </summary>
public record PartnerScanRegisteredEvent
{
    public Guid ScanId { get; init; }
    public Guid PartnerId { get; init; }
    public Guid UserId { get; init; }
    public string ScanMethod { get; init; } = string.Empty;
    public decimal PredictedDiscount { get; init; }
    public DateTime ScannedAt { get; init; }
}
