// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Application.DependencyInjection
// Assembly: RPlus.Kernel.Guard.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 82568AEC-3F33-4FE6-A0C6-A1DA0DDC1E1F
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Application.dll

using Microsoft.Extensions.DependencyInjection;
using System;

#nullable enable
namespace RPlus.Kernel.Guard.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddGuardApplication(this IServiceCollection services)
  {
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(typeof (RPlus.Kernel.Guard.Application.DependencyInjection).Assembly)));
    return services;
  }
}
