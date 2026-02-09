using MediatR;

#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed record GetBalanceQuery(string UserId) : IRequest<long>, IBaseRequest;
