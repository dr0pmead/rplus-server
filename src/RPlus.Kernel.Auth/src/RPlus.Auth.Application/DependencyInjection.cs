// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.DependencyInjection
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

#nullable enable
namespace RPlus.Auth.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddAuthApplication(this IServiceCollection services)
  {
    Assembly assembly = typeof (RPlus.Auth.Application.DependencyInjection).Assembly;
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(assembly)));
    services.AddValidatorsFromAssembly(assembly);
    return services;
  }
}
