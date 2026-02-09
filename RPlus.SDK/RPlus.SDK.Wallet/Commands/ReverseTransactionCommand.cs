using MediatR;
using RPlus.SDK.Wallet.Results;

#nullable enable
namespace RPlus.SDK.Wallet.Commands;

/// <summary>
/// Issues a compensating transaction against an existing operation.
/// </summary>
public sealed record ReverseTransactionCommand(
    string UserId,
    string OriginalOperationId,
    string Reason,
    string RequestId) : IRequest<ReverseTransactionResult>, IBaseRequest;
