using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record ValidateApiKeyQuery(
    string ApiKey,
    string ApiSecret,
    string Environment) : IRequest<ValidateApiKeyResponse>, IBaseRequest;
