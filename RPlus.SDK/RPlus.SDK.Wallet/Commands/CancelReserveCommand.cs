using MediatR;
using RPlus.SDK.Wallet.Results;

#nullable enable
namespace RPlus.SDK.Wallet.Commands;

public sealed record CancelReserveCommand(
    string UserId,
    string OperationId) : IRequest<CancelReserveResult>, IBaseRequest;
