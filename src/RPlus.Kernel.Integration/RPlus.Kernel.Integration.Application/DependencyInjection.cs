// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.DependencyInjection
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Microsoft.Extensions.DependencyInjection;
using RPlus.Kernel.Integration.Application.Services;
using System;

#nullable enable
namespace RPlus.Kernel.Integration.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddIntegrationApplication(this IServiceCollection services)
  {
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(typeof (RPlus.Kernel.Integration.Application.DependencyInjection).Assembly)));
    services.AddScoped<IIntegrationRouteResolver, IntegrationRouteResolver>();
    services.AddSingleton<IGrpcReflectionCaller, GrpcReflectionCaller>();
    services.AddScoped<IIntegrationAuditService, IntegrationAuditService>();
    services.AddSingleton<IIntegrationRateLimiter, IntegrationRateLimiter>();
    return services;
  }
}
