using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPlus.Kernel.Integration.Domain.Entities;

/// <summary>
/// Scan status for partner integration.
/// </summary>
public enum PartnerScanStatus
{
    /// <summary>Scan registered, awaiting commit from POS.</summary>
    Pending,
    
    /// <summary>Order closed successfully, discount applied.</summary>
    Committed,
    
    /// <summary>Order cancelled on POS before close.</summary>
    Cancelled,
    
    /// <summary>Scan expired without commit (24h timeout).</summary>
    Expired,
    
    /// <summary>Processing failed (limits, fraud, etc.).</summary>
    Failed
}

/// <summary>
/// Intent Journal: Records scan attempt and predicted discount.
/// Part of Double Entry bookkeeping for partner integration.
/// </summary>
[Table("partner_scans", Schema = "integration")]
public class PartnerScan
{
    /// <summary>Unique scan identifier.</summary>
    [Key]
    public Guid ScanId { get; set; } = Guid.NewGuid();

    /// <summary>Partner who received the scan.</summary>
    public Guid PartnerId { get; set; }

    /// <summary>POS terminal identifier.</summary>
    [MaxLength(255)]
    public string TerminalId { get; set; } = string.Empty;

    /// <summary>Cashier who performed the scan (for audit).</summary>
    [MaxLength(255)]
    public string? CashierId { get; set; }

    // ========== Order Context ==========

    /// <summary>External order ID (e.g., iiko Order GUID).</summary>
    public Guid OrderId { get; set; }

    /// <summary>Order total at scan time (predicted).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OrderSumPredicted { get; set; }

    // ========== Scan Data ==========

    /// <summary>RPlus User ID (resolved from QR/OTP).</summary>
    public Guid UserId { get; set; }

    /// <summary>Scan method: "qr" or "otp".</summary>
    [MaxLength(20)]
    public string ScanMethod { get; set; } = "qr";

    // ========== Predicted Discounts ==========

    /// <summary>Predicted RPlus platform discount.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PredictedUserDiscount { get; set; }

    /// <summary>Predicted partner-specific discount.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PredictedPartnerDiscount { get; set; }

    // ========== State ==========

    /// <summary>Current scan status.</summary>
    public PartnerScanStatus Status { get; set; } = PartnerScanStatus.Pending;

    /// <summary>Error reason if status is Failed.</summary>
    [MaxLength(500)]
    public string? ErrorReason { get; set; }

    // ========== Timestamps ==========

    /// <summary>When scan was registered.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When scan expires (CreatedAt + TTL).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When order was committed (if committed).</summary>
    public DateTime? CommittedAt { get; set; }

    /// <summary>When scan was cancelled (if cancelled).</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>Reason for cancellation (deleted, storned, etc.).</summary>
    [MaxLength(500)]
    public string? CancelReason { get; set; }

    // ========== Idempotency & Tracing ==========

    /// <summary>Idempotency key: SHA256(Token + OrderId).</summary>
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Distributed trace ID for observability.</summary>
    [MaxLength(64)]
    public string? TraceId { get; set; }

    // ========== Navigation ==========

    /// <summary>Partner reference.</summary>
    public IntegrationPartner? Partner { get; set; }

    /// <summary>Commit record (if order was closed).</summary>
    public PartnerCommit? Commit { get; set; }
}
