using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Infrastructure.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public sealed class AuthSchemaUpgradeHostedService : IHostedService
{
    private readonly AuthDbContext _db;
    private readonly ILogger<AuthSchemaUpgradeHostedService> _logger;

    public AuthSchemaUpgradeHostedService(AuthDbContext db, ILogger<AuthSchemaUpgradeHostedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Auth currently uses EnsureCreated. For existing DB volumes we need an idempotent upgrade path
        // to avoid schema drift when new columns are introduced.
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE IF EXISTS auth.auth_users
                    ADD COLUMN IF NOT EXISTS is_system boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS recovery_email text NULL,
                    ADD COLUMN IF NOT EXISTS is_two_factor_enabled boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS totp_secret_encrypted text NULL,
                    ADD COLUMN IF NOT EXISTS requires_password_change boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS requires_setup boolean NOT NULL DEFAULT false;
                """,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth schema upgrade failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

