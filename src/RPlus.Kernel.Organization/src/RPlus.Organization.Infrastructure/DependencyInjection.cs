// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Infrastructure.DependencyInjection
// Assembly: RPlus.Organization.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 67956CC4-52BB-48F3-9302-33FB247F5EB1
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using RPlus.Core.Options;
using RPlus.Organization.Application.Interfaces;
using RPlus.Organization.Infrastructure.Messaging.Consumers;
using RPlus.Organization.Infrastructure.Persistence;
using System;

#nullable enable
namespace RPlus.Organization.Infrastructure;

public static class DependencyInjection
{
  public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddDbContext<OrganizationDbContext>((Action<DbContextOptionsBuilder>) (options =>
        ((DbContextOptionsBuilder) options)
          .UseNpgsql(
            configuration.GetConnectionString("DefaultConnection"),
            (Action<NpgsqlDbContextOptionsBuilder>) (b =>
              ((RelationalDbContextOptionsBuilder<NpgsqlDbContextOptionsBuilder, NpgsqlOptionsExtension>) b)
                .MigrationsAssembly(typeof (OrganizationDbContext).Assembly.FullName)))
          .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
          ));
    services.AddScoped<IOrganizationDbContext>((Func<IServiceProvider, IOrganizationDbContext>) (provider => (IOrganizationDbContext) provider.GetRequiredService<OrganizationDbContext>()));
    services.Configure<KafkaOptions>((IConfiguration) configuration.GetSection("Kafka"));
    if (configuration.GetSection("Kafka").Get<KafkaOptions>()?.BootstrapServers == null && configuration["Kafka__BootstrapServers"] == null)
      configuration.GetConnectionString("Kafka");
    services.AddHostedService<UserCreatedConsumer>();
    return services;
  }
}
