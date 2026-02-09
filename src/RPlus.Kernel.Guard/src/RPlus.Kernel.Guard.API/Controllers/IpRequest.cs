// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Api.Controllers.IpRequest
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6C1F5346-815B-4C0D-BD63-391C84B5BE3F
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\ExecuteService.dll

#nullable enable
namespace RPlus.Kernel.Guard.Api.Controllers;

public sealed record IpRequest(string Ip, string? Reason, int? TtlSeconds);
