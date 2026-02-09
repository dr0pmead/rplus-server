using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using RPlus.Auth.Application.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Infrastructure.Services;

public sealed class SystemUserSeeder : IHostedService
{
    private readonly AuthDbContext _db;
    private readonly ICryptoService _crypto;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SystemUserSeeder> _logger;

    public SystemUserSeeder(
        AuthDbContext db,
        ICryptoService crypto,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SystemUserSeeder> logger)
    {
        _db = db;
        _crypto = crypto;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var loginRaw = _configuration["SYS_ADMIN_LOGIN"];
        var passwordRaw = _configuration["SYS_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(loginRaw))
            loginRaw = "admin";

        if (string.IsNullOrWhiteSpace(passwordRaw))
        {
            _logger.LogInformation("SYS_ADMIN_PASSWORD is not configured. System admin seeding is skipped.");
            return;
        }

        var login = loginRaw.Trim().ToLowerInvariant();

        // IMPORTANT:
        // SYS_ADMIN_LOGIN/SYS_ADMIN_PASSWORD are bootstrap-only.
        // Once the system admin has changed their login/password via the wizard, we must NOT recreate the old login
        // (e.g. "admin") on container restart.
        var existingSystemUsers = await _db.AuthUsers.AsNoTracking()
            .Where(u => u.IsSystem)
            .Select(u => new { u.Id, u.Login })
            .ToListAsync(cancellationToken);

        if (existingSystemUsers.Count > 0)
        {
            if (existingSystemUsers.Count > 1)
            {
                _logger.LogWarning(
                    "Multiple system users detected ({Count}). Seeding is skipped to avoid accidental privilege duplication. Logins: {Logins}",
                    existingSystemUsers.Count,
                    string.Join(", ", existingSystemUsers.Select(x => x.Login).Where(x => !string.IsNullOrWhiteSpace(x))));
                return;
            }

            _logger.LogInformation(
                "System admin already exists (login: {Login}). Seeding is skipped.",
                existingSystemUsers[0].Login ?? "<null>");

            // Ensure root registry entry exists even if the admin was created earlier but Access had no bootstrap data.
            await TryEnsureRootRegistryAsync(existingSystemUsers[0].Id, cancellationToken);
            return;
        }

        var existing = await _db.AuthUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

        if (existing is not null)
            return;

        var tenantId = Guid.Empty;
        var tenantIdRaw = _configuration["SYS_ADMIN_TENANT_ID"];
        if (!string.IsNullOrWhiteSpace(tenantIdRaw) && !Guid.TryParse(tenantIdRaw, out tenantId))
        {
            _logger.LogWarning("SYS_ADMIN_TENANT_ID is invalid; defaulting to Guid.Empty.");
            tenantId = Guid.Empty;
        }

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        // PhoneHash has a unique index; for login-only system accounts we store a stable synthetic hash.
        var phoneHash = "login:" + ComputeSha256Hex(login);

        var user = new AuthUserEntity
        {
            Id = userId,
            Login = login,
            Email = null,
            PhoneHash = phoneHash,
            PhoneEncrypted = string.Empty,
            TenantId = tenantId,
            CreatedAt = now,
            SecurityVersion = 1,
            PasswordVersion = 1,
            IsBlocked = false,

            IsSystem = true,
            RequiresPasswordChange = true,
            RequiresSetup = true,
            IsTwoFactorEnabled = false,
            RecoveryEmail = null,
            TotpSecretEncrypted = null
        };

        var salt = _crypto.GenerateSalt();
        var hash = _crypto.HashPassword(passwordRaw, salt);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.AuthUsers.Add(user);
        _db.AuthCredentials.Add(new AuthCredentialEntity
        {
            UserId = user.Id,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = now,
            ChangedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogWarning("System Admin '{Login}' was missing and has been created.", login);

        await TryEnsureRootRegistryAsync(user.Id, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TryEnsureRootRegistryAsync(Guid userId, CancellationToken ct)
    {
        var baseUrl =
            _configuration["Services:Access:Http"]
            ?? _configuration["Services__Access__Http"]
            ?? "http://rplus-kernel-access:5002";

        var bootstrapSecret =
            _configuration["RootAccess:BootstrapSecret"]
            ?? _configuration["RootAccess__BootstrapSecret"]
            ?? _configuration["ROOT_BOOTSTRAP_SECRET"]
            ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(bootstrapSecret))
        {
            _logger.LogWarning("RootAccess:BootstrapSecret is not configured. Root registry bootstrap for system admin is skipped.");
            return;
        }

        // Access may not be ready when Auth starts; retry for a short period to avoid a "no rights" admin.
        var client = _httpClientFactory.CreateClient("access-root-bootstrap");
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Remove("X-Root-Bootstrap-Secret");
        client.DefaultRequestHeaders.Add("X-Root-Bootstrap-Secret", bootstrapSecret);

        for (var attempt = 1; attempt <= 10 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                var res = await client.PostAsJsonAsync(
                    "/api/internal/root-registry/ensure",
                    new { userId = userId.ToString() },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    ct);

                if (res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Root registry ensured for system admin (attempt {Attempt}).", attempt);
                    return;
                }

                var text = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Failed to ensure root registry entry for system admin (attempt {Attempt}). Status={Status}. Body={Body}",
                    attempt,
                    (int)res.StatusCode,
                    text);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to ensure root registry entry for system admin (attempt {Attempt}).", attempt);
            }

            var delaySeconds = Math.Min(30, 1 + (attempt * attempt));
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
        }
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
