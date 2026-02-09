using Microsoft.Extensions.Logging;
using RPlus.SDK.Infrastructure.Vault;

namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods to add Vault as a configuration source.
/// </summary>
public static class VaultExtensions
{
    /// <summary>
    /// Adds HashiCorp Vault KV v2 as a configuration source.
    ///
    /// <para>
    /// Reads from:
    /// <list type="bullet">
    ///   <item><c>Vault__Address</c> — Vault server URL (default: <c>http://kernel-vault:8200</c>)</item>
    ///   <item><c>Vault__Token</c> — Dev mode token (used if RoleId is empty)</item>
    ///   <item><c>Vault__RoleId</c> / <c>Vault__SecretId</c> — AppRole auth (production)</item>
    ///   <item><c>Vault__ServiceName</c> — Service name for per-service secret path</item>
    ///   <item><c>Vault__Enabled</c> — Set to <c>false</c> to skip Vault entirely</item>
    /// </list>
    /// </para>
    ///
    /// Usage in Program.cs:
    /// <code>
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.Configuration.AddVault("auth"); // service name
    /// </code>
    /// </summary>
    /// <param name="configurationBuilder">The configuration builder to add Vault to.</param>
    /// <param name="serviceName">
    /// The logical name of this service (e.g., "auth", "loyalty").
    /// Used to load service-specific secrets from <c>secret/rplus/{serviceName}</c>.
    /// </param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddVault(
        this IConfigurationBuilder configurationBuilder,
        string serviceName,
        ILoggerFactory? loggerFactory = null)
    {
        // Build interim config to read Vault connection settings from env/appsettings
        var interimConfig = configurationBuilder.Build();

        var enabled = interimConfig["Vault:Enabled"]
                      ?? interimConfig["Vault__Enabled"]
                      ?? "true";
        if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
            return configurationBuilder;

        var address = interimConfig["Vault:Address"]
                      ?? interimConfig["Vault__Address"]
                      ?? interimConfig["VAULT_ADDR"]
                      ?? "http://kernel-vault:8200";

        var token = interimConfig["Vault:Token"]
                    ?? interimConfig["Vault__Token"]
                    ?? interimConfig["VAULT_TOKEN"];

        var roleId = interimConfig["Vault:RoleId"]
                     ?? interimConfig["Vault__RoleId"];

        var secretId = interimConfig["Vault:SecretId"]
                       ?? interimConfig["Vault__SecretId"];

        // Skip if no auth method is configured
        if (string.IsNullOrWhiteSpace(token) &&
            (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId)))
        {
            var logger = loggerFactory?.CreateLogger("Vault");
            logger?.LogWarning(
                "Vault: no Token or RoleId+SecretId configured. Skipping Vault configuration. " +
                "Set Vault__Token or Vault__RoleId+Vault__SecretId to enable.");
            return configurationBuilder;
        }

        var mountPoint = interimConfig["Vault:MountPoint"]
                         ?? interimConfig["Vault__MountPoint"]
                         ?? "secret";

        // Custom paths or defaults
        var pathsRaw = interimConfig["Vault:Paths"]
                       ?? interimConfig["Vault__Paths"];

        var paths = !string.IsNullOrWhiteSpace(pathsRaw)
            ? pathsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { "rplus/shared", "rplus/databases", $"rplus/{serviceName}" };

        configurationBuilder.Add(new VaultConfigurationSource
        {
            VaultAddress = address,
            Token = token,
            RoleId = roleId,
            SecretId = secretId,
            ServiceName = serviceName,
            MountPoint = mountPoint,
            SecretPaths = paths,
            Logger = loggerFactory?.CreateLogger<VaultConfigurationProvider>()
        });

        return configurationBuilder;
    }
}
