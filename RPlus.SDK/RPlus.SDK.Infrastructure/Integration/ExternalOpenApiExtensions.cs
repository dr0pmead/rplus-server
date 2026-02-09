// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.ExternalOpenApiExtensions
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using RPlus.SDK.Core.Abstractions;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public static class ExternalOpenApiExtensions
{
  public static IServiceCollection AddExternalOpenApi(this IServiceCollection services)
  {
    services.AddSwaggerGen((Action<SwaggerGenOptions>) (options =>
    {
      SwaggerGenOptionsExtensions.SwaggerDoc(options, "external", new OpenApiInfo()
      {
        Title = "RPlus External API",
        Version = "v1"
      });
      options.DocInclusionPredicate((Func<string, ApiDescription, bool>) ((_, api) => api.ActionDescriptor.EndpointMetadata.OfType<ExternalAttribute>().Any<ExternalAttribute>()));
    }));
    return services;
  }
}
