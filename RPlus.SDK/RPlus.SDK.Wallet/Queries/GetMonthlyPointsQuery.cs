using MediatR;

#nullable enable
namespace RPlus.SDK.Wallet.Queries;

public sealed record GetMonthlyPointsQuery(
    string UserId,
    int Year,
    int Month,
    string[]? SourceTypes = null) : IRequest<GetMonthlyPointsResult>, IBaseRequest;

public sealed record GetMonthlyPointsResult(
    long TotalPoints,
    int TransactionCount,
    bool Success,
    string? Error = null);
