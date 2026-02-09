using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Organization.DTOs;

#nullable enable
namespace RPlus.SDK.Organization.Queries;

public sealed record GetOrganizationQuery(Guid OrganizationId) : IRequest<OrganizationDto>, IBaseRequest;
