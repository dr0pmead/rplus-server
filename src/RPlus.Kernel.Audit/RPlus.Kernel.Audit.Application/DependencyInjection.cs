// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Application.DependencyInjection
// Assembly: RPlus.Kernel.Audit.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 47CD16EE-F06C-4FE6-B257-E7E3B39F4C9C
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Application.dll

using Microsoft.Extensions.DependencyInjection;
using System;

#nullable enable
namespace RPlus.Kernel.Audit.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddAuditApplication(this IServiceCollection services)
  {
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(typeof (RPlus.Kernel.Audit.Application.DependencyInjection).Assembly)));
    return services;
  }
}
