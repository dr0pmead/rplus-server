using MediatR;
using RPlus.SDK.Wallet.Results;

#nullable enable
namespace RPlus.SDK.Wallet.Commands;

/// <summary>
/// Issues a signed balance mutation for a user. Positive amounts accrue, negatives debit.
/// </summary>
public sealed record AccruePointsCommand(
    string UserId,
    long Amount,
    string OperationId,
    string Source,
    string Description,
    string Metadata,
    long Timestamp,
    string RequestId,
    string Signature,
    string? SourceType = null,
    string? SourceCategory = null) : IRequest<AccruePointsResult>, IBaseRequest;

