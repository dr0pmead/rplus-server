// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Features.Partners.Commands.CreatePartnerCommand
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using MediatR;
using System;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Features.Partners.Commands;

public record CreatePartnerCommand(string Name, string Description, bool IsDiscountPartner, decimal? DiscountPartner, string? AccessLevel) : 
  IRequest<Guid>,
  IBaseRequest
;
