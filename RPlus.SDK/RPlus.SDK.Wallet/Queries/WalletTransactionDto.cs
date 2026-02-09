#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed class WalletTransactionDto
{
    public string OperationId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long BalanceBefore { get; set; }
    public long BalanceAfter { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public long ProcessedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
}
