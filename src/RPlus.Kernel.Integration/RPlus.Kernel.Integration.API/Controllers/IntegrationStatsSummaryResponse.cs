// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.IntegrationStatsSummaryResponse
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using RPlus.Kernel.Integration.Infrastructure.Services;
using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

public sealed record IntegrationStatsSummaryResponse(
  DateTime From,
  DateTime To,
  IReadOnlyList<IntegrationStatsSummaryRow> Items)
;
