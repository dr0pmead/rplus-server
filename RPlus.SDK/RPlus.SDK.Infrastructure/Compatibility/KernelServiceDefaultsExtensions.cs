// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Infrastructure.Extensions.KernelServiceDefaultsExtensions
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#nullable enable
namespace RPlus.Kernel.Infrastructure.Extensions;

public static class KernelServiceDefaultsExtensions
{
  public static IServiceCollection AddKernelServiceDefaults(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();
    services.AddHealthChecks();
    return services;
  }

  public static WebApplication UseKernelServiceDefaults(this WebApplication app)
  {
    app.UseSwagger();
    app.UseSwaggerUI();
    return app;
  }
}
