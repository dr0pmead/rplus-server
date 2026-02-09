// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IntegrationAdminService
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Application.Features.Partners.Queries;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlusGrpc.Integration.Admin;
using System;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class IntegrationAdminService : RPlusGrpc.Integration.Admin.IntegrationAdminService.IntegrationAdminServiceBase
{
  private readonly IMediator _mediator;
  private readonly ILogger<IntegrationAdminService> _logger;

  public IntegrationAdminService(IMediator mediator, ILogger<IntegrationAdminService> logger)
  {
    this._mediator = mediator;
    this._logger = logger;
  }

  public override async Task<ListPartnersResponse> ListPartners(
    ListPartnersRequest request,
    ServerCallContext context)
  {
    ListPartnersResult listPartnersResult = await this._mediator.Send<ListPartnersResult>((IRequest<ListPartnersResult>) new ListPartnersQuery(request.Page > 0 ? request.Page : 1, request.PageSize > 0 ? request.PageSize : 10, request.Search));
    ListPartnersResponse partnersResponse = new ListPartnersResponse();
    partnersResponse.TotalCount = listPartnersResult.TotalCount;
    partnersResponse.Items.AddRange(listPartnersResult.Items.Select<IntegrationPartner, PartnerDto>((Func<IntegrationPartner, PartnerDto>) (p => new PartnerDto()
    {
      Id = p.Id.ToString(),
      Name = p.Name,
      Description = p.Description,
      IsDiscountPartner = p.IsDiscountPartner,
      IsActive = p.IsActive,
      CreatedAt = Timestamp.FromDateTime(p.CreatedAt.ToUniversalTime()),
      ApiKeyCount = 0
    })));
    return partnersResponse;
  }

  public override async Task<PartnerDto> GetPartner(
    GetPartnerRequest request,
    ServerCallContext context)
  {
    Guid result;
    if (!Guid.TryParse(request.Id, out result))
      throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ID format"));
    IntegrationPartner integrationPartner = await this._mediator.Send<IntegrationPartner>((IRequest<IntegrationPartner>) new GetPartnerQuery(result));
    if (integrationPartner == null)
      throw new RpcException(new Status(StatusCode.NotFound, "Partner not found"));
    return new PartnerDto()
    {
      Id = integrationPartner.Id.ToString(),
      Name = integrationPartner.Name,
      Description = integrationPartner.Description,
      IsDiscountPartner = integrationPartner.IsDiscountPartner,
      IsActive = integrationPartner.IsActive,
      CreatedAt = Timestamp.FromDateTime(integrationPartner.CreatedAt.ToUniversalTime()),
      ApiKeyCount = 0
    };
  }
}
