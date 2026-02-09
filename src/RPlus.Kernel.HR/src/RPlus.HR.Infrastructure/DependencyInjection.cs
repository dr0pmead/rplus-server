using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPlus.HR.Application.Interfaces;
using RPlus.HR.Infrastructure.Persistence;

namespace RPlus.HR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IHrActorContext, NullHrActorContext>();

        var cs = configuration.GetConnectionString("DefaultConnection")
                 ?? configuration["DB_CONNECTION_STRING"]
                 ?? "Host=rplus-kernel-db;Database=hr;Username=postgres;Password=postgres";

        services.AddDbContext<HrDbContext>(options =>
            options.UseNpgsql(cs, b => b.MigrationsAssembly(typeof(HrDbContext).Assembly.FullName))
                   .UseSnakeCaseNamingConvention()
                   // Do not block startup on model diff; migrations are applied on boot.
                   .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IHrDbContext>(sp => sp.GetRequiredService<HrDbContext>());
        return services;
    }

    private sealed class NullHrActorContext : IHrActorContext
    {
        public Guid? ActorUserId => null;
        public string ActorType => "system";
        public string? ActorService => null;
    }
}
