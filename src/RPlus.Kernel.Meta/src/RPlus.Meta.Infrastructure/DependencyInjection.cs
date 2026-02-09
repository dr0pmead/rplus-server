using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RPlus.Meta.Infrastructure.Persistence;

namespace RPlus.Meta.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMetaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? configuration.GetConnectionString("Database")
                                ?? configuration.GetConnectionString("Postgres")
                                ?? configuration["ConnectionStrings__DefaultConnection"]
                                ?? configuration["META_DB_CONNECTION"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Meta database connection string is missing.");

        services.AddDbContext<MetaDbContext>(options =>
        {
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IMetaDbContext>(sp => sp.GetRequiredService<MetaDbContext>());
        return services;
    }
}
