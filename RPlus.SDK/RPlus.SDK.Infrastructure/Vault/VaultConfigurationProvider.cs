using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Token;

namespace RPlus.SDK.Infrastructure.Vault;

/// <summary>
/// Loads secrets from HashiCorp Vault KV v2 into <see cref="IConfiguration"/>.
///
/// Reads from two paths:
/// <list type="bullet">
///   <item><c>secret/rplus/shared</c> — secrets shared by all services</item>
///   <item><c>secret/rplus/{serviceName}</c> — service-specific secrets</item>
/// </list>
///
/// Keys are flattened into Configuration with their original names.
/// Example: Vault key "KERNEL_DB_PASSWORD" at secret/rplus/shared
///          → accessible as Configuration["KERNEL_DB_PASSWORD"]
/// </summary>
public sealed class VaultConfigurationProvider : ConfigurationProvider
{
    private readonly string _vaultAddress;
    private readonly string? _token;
    private readonly string? _roleId;
    private readonly string? _secretId;
    private readonly string _serviceName;
    private readonly string _mountPoint;
    private readonly string[] _paths;
    private readonly ILogger? _logger;

    public VaultConfigurationProvider(VaultConfigurationSource source)
    {
        _vaultAddress = source.VaultAddress;
        _token = source.Token;
        _roleId = source.RoleId;
        _secretId = source.SecretId;
        _serviceName = source.ServiceName;
        _mountPoint = source.MountPoint;
        _paths = source.SecretPaths;
        _logger = source.Logger;
    }

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"[Vault] Load() called. Address={_vaultAddress}, HasToken={!string.IsNullOrWhiteSpace(_token)}, Service={_serviceName}, Paths={string.Join(",", _paths)}");

        try
        {
            var client = CreateClient();

            foreach (var path in _paths)
            {
                var resolvedPath = path.Replace("{service}", _serviceName, StringComparison.OrdinalIgnoreCase);
                try
                {
                    Console.WriteLine($"[Vault] Reading {_mountPoint}/{resolvedPath}...");
                    var secret = client.V1.Secrets.KeyValue.V2
                        .ReadSecretAsync(resolvedPath, mountPoint: _mountPoint)
                        .GetAwaiter().GetResult();

                    if (secret?.Data?.Data is null)
                    {
                        Console.WriteLine($"[Vault] WARNING: path '{resolvedPath}' returned null data");
                        _logger?.LogWarning("Vault: path '{Path}' returned null data", resolvedPath);
                        continue;
                    }

                    foreach (var kv in secret.Data.Data)
                    {
                        var key = kv.Key;
                        var value = kv.Value?.ToString();

                        // Replace __ with : for .NET configuration hierarchy
                        // e.g., "ConnectionStrings__DefaultConnection" → "ConnectionStrings:DefaultConnection"
                        key = key.Replace("__", ":");

                        data[key] = value;
                    }

                    Console.WriteLine($"[Vault] ✅ Loaded {secret.Data.Data.Count} keys from {_mountPoint}/{resolvedPath}");
                    _logger?.LogInformation(
                        "Vault: loaded {Count} keys from {Mount}/{Path}",
                        secret.Data.Data.Count, _mountPoint, resolvedPath);
                }
                catch (VaultSharp.Core.VaultApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[Vault] WARNING: path '{_mountPoint}/{resolvedPath}' not found — skipping");
                    _logger?.LogWarning("Vault: path '{Mount}/{Path}' not found — skipping", _mountPoint, resolvedPath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Vault] ❌ FAILED to load secrets from {_vaultAddress}: {ex.Message}");
            _logger?.LogError(ex, "Vault: failed to load secrets from {Address}. Services will use fallback env/config values", _vaultAddress);
            return;
        }

        Console.WriteLine($"[Vault] Total keys loaded: {data.Count}");
        Data = data!;
    }

    private IVaultClient CreateClient()
    {
        IAuthMethodInfo authMethod;

        if (!string.IsNullOrWhiteSpace(_roleId) && !string.IsNullOrWhiteSpace(_secretId))
        {
            // Production: AppRole authentication
            authMethod = new AppRoleAuthMethodInfo(_roleId, _secretId);
        }
        else if (!string.IsNullOrWhiteSpace(_token))
        {
            // Dev mode: Token authentication
            authMethod = new TokenAuthMethodInfo(_token);
        }
        else
        {
            throw new InvalidOperationException(
                "Vault configuration requires either Vault__Token (dev) or Vault__RoleId + Vault__SecretId (production).");
        }

        var settings = new VaultClientSettings(_vaultAddress, authMethod)
        {
            UseVaultTokenHeaderInsteadOfAuthorizationHeader = true,
            ContinueAsyncTasksOnCapturedContext = false
        };

        return new VaultClient(settings);
    }
}

/// <summary>
/// Configuration source for Vault. Used by <see cref="VaultExtensions.AddVault"/>.
/// </summary>
public sealed class VaultConfigurationSource : IConfigurationSource
{
    public string VaultAddress { get; set; } = "http://kernel-vault:8200";
    public string? Token { get; set; }
    public string? RoleId { get; set; }
    public string? SecretId { get; set; }
    public string ServiceName { get; set; } = "unknown";
    public string MountPoint { get; set; } = "secret";
    public string[] SecretPaths { get; set; } = ["rplus/shared", "rplus/{service}"];
    public ILogger? Logger { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new VaultConfigurationProvider(this);
}
