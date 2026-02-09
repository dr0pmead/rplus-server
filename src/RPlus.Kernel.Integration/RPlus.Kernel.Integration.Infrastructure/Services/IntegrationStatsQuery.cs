// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Infrastructure.Services.IntegrationStatsQuery
// Assembly: RPlus.Kernel.Integration.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 62B7ABAE-4A2B-4AF9-BC30-AC25C64E0B51
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Infrastructure.dll

using System;

#nullable enable
namespace RPlus.Kernel.Integration.Infrastructure.Services;

public sealed record IntegrationStatsQuery(
  Guid? PartnerId,
  string? Scope,
  string? Endpoint,
  string? Env,
  DateTime From,
  DateTime To)
;
