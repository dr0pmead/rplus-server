// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Infrastructure.Services.GuardMaintenanceStatus
// Assembly: RPlus.Kernel.Guard.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: DF97D949-B080-4EE7-A993-4CFFBB255DD1
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Infrastructure.dll

using System;

#nullable enable
namespace RPlus.Kernel.Guard.Infrastructure.Services;

public sealed record GuardMaintenanceStatus(bool Enabled, string? Reason, TimeSpan? Ttl);
