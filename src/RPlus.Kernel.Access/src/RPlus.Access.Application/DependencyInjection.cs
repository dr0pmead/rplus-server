// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Application.DependencyInjection
// Assembly: RPlus.Access.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 42B68179-0F94-443C-B8AC-3FE1745E13E8
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Application.dll

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RPlus.Access.Application.Interfaces;
using RPlus.Access.Application.Services;
using System;
using System.Reflection;

#nullable enable
namespace RPlus.Access.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())));
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    services.AddScoped<IPolicyEvaluator, PolicyEvaluator>();
    services.AddScoped<IAccessContextBuilder, AccessContextBuilder>();
    services.AddScoped<IRiskEvaluator, RiskEvaluator>();
    services.AddScoped<IPermissionRegistry, PermissionRegistry>();
    services.AddScoped<IEffectiveRightsService, EffectiveRightsService>();
    services.AddScoped<IIntegrationPermissionService, IntegrationPermissionService>();
    services.AddScoped<AccessCheckService>();
    return services;
  }
}
