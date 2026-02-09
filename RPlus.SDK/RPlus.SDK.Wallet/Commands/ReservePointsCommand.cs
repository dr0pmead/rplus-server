using MediatR;
using RPlus.SDK.Wallet.Results;

#nullable enable
namespace RPlus.SDK.Wallet.Commands;

/// <summary>
/// Places funds into a reserved state so later commits can settle them.
/// </summary>
public sealed record ReservePointsCommand(
    string UserId,
    long Amount,
    string OperationId,
    string Source,
    string Description,
    string Metadata,
    long Timestamp,
    string RequestId,
    string Signature) : IRequest<ReservePointsResult>, IBaseRequest;
