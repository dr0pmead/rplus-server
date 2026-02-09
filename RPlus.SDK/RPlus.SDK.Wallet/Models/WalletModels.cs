using System;

#nullable enable
namespace RPlus.SDK.Wallet.Models;

public class Wallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] BalanceEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] ReservedBalanceEncrypted { get; set; } = Array.Empty<byte>();
    public string BalanceKeyId { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public byte[] AmountEncrypted { get; set; } = Array.Empty<byte>();
    public string Status { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    
    // New fields for motivational points aggregation
    public string? SourceType { get; set; }      // "activity", "purchase", "referral", "admin"
    public string? SourceCategory { get; set; }  // More detailed category
    public int Year { get; set; }                // For fast indexing
    public int Month { get; set; }               // For monthly aggregations
}

