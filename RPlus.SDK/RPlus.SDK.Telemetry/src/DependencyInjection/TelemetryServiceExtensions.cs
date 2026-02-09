using Microsoft.Extensions.DependencyInjection;
using RPlus.SDK.Telemetry.Abstractions;
using RPlus.SDK.Telemetry.Implementation;

namespace RPlus.SDK.Telemetry.DependencyInjection;

public static class TelemetryServiceExtensions
{
    public static IServiceCollection AddRPlusTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryPublisher, KafkaTelemetryPublisher>();
        services.AddSingleton<IMetricsService, DefaultMetricsService>();
        services.AddSingleton<ITelemetryService, DefaultTelemetryService>();
        
        return services;
    }
}
