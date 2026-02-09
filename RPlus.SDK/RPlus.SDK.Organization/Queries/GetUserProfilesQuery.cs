using System;
using System.Collections.Generic;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Organization.DTOs;

#nullable enable
namespace RPlus.SDK.Organization.Queries;

public sealed record GetUserProfilesQuery(List<Guid> UserIds) : IRequest<Dictionary<Guid, UserProfileDto>>, IBaseRequest;
