using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Workers;

internal sealed class PermissionRegistrarWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionRegistrarWorker> _logger;

    public PermissionRegistrarWorker(IServiceScopeFactory scopeFactory, ILogger<PermissionRegistrarWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // give the API a short window to start before registering permissions
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        using var scope = _scopeFactory.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var client = scope.ServiceProvider.GetRequiredService<AccessService.AccessServiceClient>();

        var permissions = configuration
            .GetSection("Integration:Permissions")
            .Get<string[]>() ?? Array.Empty<string>();

        if (permissions.Length == 0)
        {
            _logger.LogInformation("No integration permissions configured for registration");
            return;
        }

        var applicationId = configuration["Integration:ApplicationId"] ?? "integration";

        foreach (var permissionId in permissions.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var trimmedId = permissionId.Trim();

                await client.RegisterPermissionAsync(new RegisterPermissionRequest
                {
                    PermissionId = trimmedId,
                    ApplicationId = applicationId
                }, cancellationToken: stoppingToken).ConfigureAwait(false);

                await client.ActivatePermissionAsync(new ActivatePermissionRequest
                {
                    PermissionId = trimmedId,
                    ApplicationId = applicationId
                }, cancellationToken: stoppingToken).ConfigureAwait(false);

                _logger.LogInformation("Registered integration permission {PermissionId}", trimmedId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register integration permission {PermissionId}", permissionId);
            }
        }
    }
}
