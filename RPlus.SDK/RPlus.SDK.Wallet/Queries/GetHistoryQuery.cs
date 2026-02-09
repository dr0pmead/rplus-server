using MediatR;

#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed record GetHistoryQuery(
    string UserId,
    int Limit,
    string? Cursor,
    string? Source) : IRequest<GetHistoryResult>, IBaseRequest;
