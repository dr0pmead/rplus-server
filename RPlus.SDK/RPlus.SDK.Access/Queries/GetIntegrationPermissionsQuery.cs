using System;
using System.Collections.Generic;
using MediatR;
using RPlus.SDK.Access.Enums;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Access.Queries;

public sealed record GetIntegrationPermissionsQuery(
    Guid PartnerId,
    Guid ApiKeyId,
    Dictionary<string, string>? ContextSignals = null) : IRequest<GetIntegrationPermissionsResponse>, IBaseRequest;
