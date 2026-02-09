using MediatR;

#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed record CheckStatusQuery(
    string UserId,
    string? OperationId) : IRequest<CheckStatusResult>, IBaseRequest;
