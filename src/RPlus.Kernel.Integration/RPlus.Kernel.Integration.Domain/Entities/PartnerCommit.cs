using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPlus.Kernel.Integration.Domain.Entities;

/// <summary>
/// Fact Journal: Financial record of order closure.
/// Created only when POS closes the check successfully.
/// Part of Double Entry bookkeeping for partner integration.
/// </summary>
[Table("partner_commits", Schema = "integration")]
public class PartnerCommit
{
    /// <summary>Unique commit identifier.</summary>
    [Key]
    public Guid CommitId { get; set; } = Guid.NewGuid();

    /// <summary>Reference to the original scan (Intent).</summary>
    public Guid ScanId { get; set; }

    /// <summary>POS terminal identifier.</summary>
    [MaxLength(255)]
    public string? Terminal { get; set; }

    // ========== Financial Facts ==========

    /// <summary>Final order total after discounts.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalOrderTotal { get; set; }

    /// <summary>Order total before any discounts.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalOrderTotalBeforeDiscounts { get; set; }

    /// <summary>Final RPlus discount percent.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal FinalUserDiscount { get; set; }

    /// <summary>Final partner discount percent.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal FinalPartnerDiscount { get; set; }

    /// <summary>Actual RPlus discount amount in currency.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? UserDiscountAmount { get; set; }

    /// <summary>Actual partner discount amount in currency.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PartnerDiscountAmount { get; set; }

    // ========== QR Discount Metadata ==========

    /// <summary>QR discount type ID from plugin config.</summary>
    [MaxLength(255)]
    public string? QrDiscountTypeId { get; set; }

    /// <summary>QR discount percent applied.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? QrDiscountPercent { get; set; }

    /// <summary>QR discount sum applied.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? QrDiscountSum { get; set; }

    // ========== Receipt Metadata ==========

    /// <summary>Printed receipt number.</summary>
    [MaxLength(100)]
    public string? ChequeNumber { get; set; }

    /// <summary>Fiscal identifier (FD/FP).</summary>
    [MaxLength(100)]
    public string? FiscalId { get; set; }

    /// <summary>When order was closed on POS.</summary>
    public DateTime ClosedAt { get; set; }

    // ========== Full Dump (Analytics & Anti-Fraud) ==========

    /// <summary>Order items JSON (what was purchased).</summary>
    [Column(TypeName = "jsonb")]
    public string? ItemsJson { get; set; }

    /// <summary>Payment methods JSON (card/cash split).</summary>
    [Column(TypeName = "jsonb")]
    public string? PaymentsJson { get; set; }

    /// <summary>Discount lines JSON (what discounts were applied).</summary>
    [Column(TypeName = "jsonb")]
    public string? DiscountsJson { get; set; }

    // ========== Wallet Integration ==========

    /// <summary>Whether wallet operation was processed.</summary>
    public bool WalletProcessed { get; set; } = false;

    /// <summary>Wallet transaction ID (if processed).</summary>
    public Guid? WalletTransactionId { get; set; }

    /// <summary>When wallet was processed.</summary>
    public DateTime? WalletProcessedAt { get; set; }

    // ========== Timestamps ==========

    /// <summary>When commit record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Idempotency key (usually same as ScanId).</summary>
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;

    // ========== Navigation ==========

    /// <summary>Parent scan (Intent).</summary>
    public PartnerScan? Scan { get; set; }
}
