using System;
using System.Collections.Generic;
using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Organization.Commands;

public sealed record CreateOrganizationCommand(
    Guid? ParentId,
    string Name,
    string Description,
    string? MetadataJson,
    string? RulesJson,
    List<Guid> Leaders,
    List<Guid> Deputies,
    List<Guid> Members) : IRequest<CreateOrganizationResponse>, IBaseRequest;
