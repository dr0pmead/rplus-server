using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RPlus.SDK.Eventing.SchemaRegistry;

namespace RPlus.SDK.Infrastructure.SchemaRegistry;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventSchemaRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EventSchemaRegistryOptions>()
            .Bind(configuration.GetSection(EventSchemaRegistryOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IEventSchemaRegistryReader, EventSchemaRegistryReader>();
        return services;
    }

    public static IServiceCollection AddEventSchemaRegistryPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEventSchemaRegistry(configuration);
        services.AddSingleton<IEventSchemaPublisher, EventSchemaRegistryPublisher>();
        services.AddHostedService<EventSchemaPublisherHostedService>();
        return services;
    }
}

