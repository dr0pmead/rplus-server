// Decompiled with JetBrains decompiler
// Type: RPlus.Users.Application.DependencyInjection
// Assembly: RPlus.Users.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 48B001A8-2E15-4980-831E-0027ECCC6407
// Assembly location: F:\RPlus Framework\Recovery\users\RPlus.Users.Application.dll

using Microsoft.Extensions.DependencyInjection;
using System;

#nullable enable
namespace RPlus.Users.Application;

public static class DependencyInjection
{
  public static IServiceCollection AddApplication(this IServiceCollection services)
  {
    services.AddMediatR((Action<MediatRServiceConfiguration>) (cfg => cfg.RegisterServicesFromAssembly(typeof (RPlus.Users.Application.DependencyInjection).Assembly)));
    return services;
  }
}
