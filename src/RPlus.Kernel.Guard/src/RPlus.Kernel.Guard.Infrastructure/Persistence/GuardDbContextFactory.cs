using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RPlus.Kernel.Guard.Infrastructure.Persistence;

public sealed class GuardDbContextFactory : IDesignTimeDbContextFactory<GuardDbContext>
{
    public GuardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GuardDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("GUARD_MIGRATION_CONNECTION_STRING")
            ?? "Host=localhost;Database=guard;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new GuardDbContext(optionsBuilder.Options);
    }
}
