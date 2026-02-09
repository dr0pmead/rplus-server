// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.CreatePartnerRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

public record CreatePartnerRequest(string Name, string? Description, bool IsDiscountPartner, decimal? DiscountPartner, string? AccessLevel);
