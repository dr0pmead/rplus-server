using System;
using RPlus.SDK.Wallet.Models;

#nullable enable
namespace RPlus.Wallet.Domain.Entities;

public class WalletTransaction : RPlus.SDK.Wallet.Models.WalletTransaction
{
    public byte[] BalanceBeforeEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] BalanceAfterEncrypted { get; set; } = Array.Empty<byte>();
    public string Source { get; set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public byte[]? DescriptionEncrypted { get; private set; }
    public byte[]? MetadataEncrypted { get; private set; }

    private WalletTransaction()
    {
    }

    /// <summary>
    /// Creates a new wallet transaction.
    /// </summary>
    /// <param name="timestamp">Optional timestamp for backdating. If provided, Year/Month are derived from this timestamp.</param>
    public static WalletTransaction Create(
        Guid userId,
        string operationId,
        string requestId,
        byte[] amountEncrypted,
        byte[] balanceBefore,
        byte[] balanceAfter,
        string source,
        string status,
        string keyId,
        byte[]? description = null,
        byte[]? metadata = null,
        string? sourceType = null,
        string? sourceCategory = null,
        DateTime? timestamp = null)
    {
        var now = DateTime.UtcNow;
        // Use provided timestamp for Year/Month (for backdating), but always use 'now' for CreatedAt/ProcessedAt
        var effectiveDate = timestamp ?? now;
        return new WalletTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OperationId = operationId,
            RequestId = requestId,
            AmountEncrypted = amountEncrypted,
            BalanceBeforeEncrypted = balanceBefore,
            BalanceAfterEncrypted = balanceAfter,
            Source = source,
            Status = status,
            KeyId = keyId,
            DescriptionEncrypted = description,
            MetadataEncrypted = metadata,
            SourceType = sourceType,
            SourceCategory = sourceCategory,
            Year = effectiveDate.Year,
            Month = effectiveDate.Month,
            CreatedAt = now,
            ProcessedAt = status == "Completed" ? now : null
        };
    }

    public void MarkError(string errorCode)
    {
        ErrorCode = errorCode;
        ProcessedAt = DateTime.UtcNow;
    }
}

