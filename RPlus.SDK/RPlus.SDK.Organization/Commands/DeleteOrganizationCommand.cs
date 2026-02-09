using System;
using MediatR;
using RPlus.SDK.Core.Abstractions;

#nullable enable
namespace RPlus.SDK.Organization.Commands;

public sealed record DeleteOrganizationCommand(Guid OrganizationId) : IRequest<DeleteOrganizationResponse>, IBaseRequest;
