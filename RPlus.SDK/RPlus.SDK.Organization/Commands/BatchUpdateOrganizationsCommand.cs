using System.Collections.Generic;
using System.Collections.Generic;
using MediatR;
using RPlus.SDK.Core.Abstractions;
using RPlus.SDK.Organization.DTOs;

#nullable enable
namespace RPlus.SDK.Organization.Commands;

public sealed record BatchUpdateOrganizationsCommand(List<BatchUpdateItemDto> Updates) : IRequest<BatchUpdateOrganizationsResponse>, IBaseRequest;
