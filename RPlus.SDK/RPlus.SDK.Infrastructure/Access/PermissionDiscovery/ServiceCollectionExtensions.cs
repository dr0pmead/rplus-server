using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRPlusPermissionManifestPublisher(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PermissionManifestPublisherOptions>? configure = null)
    {
        services.Configure<PermissionManifestPublisherOptions>(configuration.GetSection(PermissionManifestPublisherOptions.SectionName));
        if (configure != null)
            services.PostConfigure(configure);

        services.AddHostedService<PermissionManifestPublisherHostedService>();
        return services;
    }
}

