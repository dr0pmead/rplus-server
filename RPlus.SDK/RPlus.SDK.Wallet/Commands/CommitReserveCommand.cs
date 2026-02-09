using MediatR;
using RPlus.SDK.Wallet.Results;

#nullable enable
namespace RPlus.SDK.Wallet.Commands;

/// <summary>
/// Converts a pending reservation into a completed debit.
/// </summary>
public sealed record CommitReserveCommand(
    string UserId,
    string OperationId) : IRequest<CommitReserveResult>, IBaseRequest;
