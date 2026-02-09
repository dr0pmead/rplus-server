#nullable enable
using System.Collections.Generic;

namespace RPlus.SDK.Wallet.Queries;

public sealed record GetHistoryResult(
    IReadOnlyList<WalletTransactionDto> Items,
    string NextCursor);
