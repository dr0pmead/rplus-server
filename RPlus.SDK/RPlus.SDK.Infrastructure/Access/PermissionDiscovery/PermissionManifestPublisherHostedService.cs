using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlusGrpc.Access;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.SDK.Infrastructure.Access.PermissionDiscovery;

public sealed class PermissionManifestPublisherHostedService : IHostedService
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IHostEnvironment _environment;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<PermissionManifestPublisherOptions> _options;
    private readonly ILogger<PermissionManifestPublisherHostedService> _logger;

    public PermissionManifestPublisherHostedService(
        EndpointDataSource endpointDataSource,
        IHostEnvironment environment,
        IHostApplicationLifetime lifetime,
        IOptions<PermissionManifestPublisherOptions> options,
        ILogger<PermissionManifestPublisherHostedService> logger)
    {
        _endpointDataSource = endpointDataSource;
        _environment = environment;
        _lifetime = lifetime;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value ?? new PermissionManifestPublisherOptions();
        if (!options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            _logger.LogWarning("Permission manifest publishing skipped: ServiceName is empty");
            return;
        }

        // MVC controller endpoints may be populated only after the app has started.
        // Publish on ApplicationStarted with a small bounded retry loop (fail-open).
        _lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(() => PublishWithRetryAsync(options, CancellationToken.None));
            _ = Task.Run(() => PublishLoopAsync(options, CancellationToken.None));
        });
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static int NormalizeIntervalSeconds(PermissionManifestPublisherOptions options)
    {
        if (options.PublishIntervalSeconds <= 0)
            return 0;

        return Math.Max(5, options.PublishIntervalSeconds);
    }

    private async Task PublishWithRetryAsync(PermissionManifestPublisherOptions options, CancellationToken ct)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var request = PermissionManifestDiscovery.Build(_endpointDataSource, options);
                if (request.Permissions.Count == 0)
                {
                    if (attempt == maxAttempts)
                    {
                        _logger.LogInformation("Permission manifest has 0 permissions for service {ServiceName}", options.ServiceName);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), ct);
                    continue;
                }

                using var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false
                };

                using var httpClient = new HttpClient(handler);
                using var channel = GrpcChannel.ForAddress(options.AccessGrpcAddress, new GrpcChannelOptions
                {
                    HttpClient = httpClient
                });

                var client = new AccessService.AccessServiceClient(channel);

                Metadata? headers = null;
                if (!string.IsNullOrWhiteSpace(options.SharedSecret))
                {
                    headers = new Metadata
                    {
                        { "x-rplus-service-secret", options.SharedSecret.Trim() }
                    };
                }
                else if (!_environment.IsDevelopment())
                {
                    _logger.LogWarning("Permission manifest publishing has no SharedSecret configured in non-development environment");
                }

                var response = await client.UpsertPermissionManifestAsync(request, headers, cancellationToken: ct);
                _logger.LogInformation(
                    "Published permission manifest: service={ServiceName} app={AppId} total={Total} upserted={Upserted} deprecated={Deprecated}",
                    options.ServiceName,
                    options.ApplicationId,
                    request.Permissions.Count,
                    response.Upserted,
                    response.Deprecated);
                return;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
            {
                // Don't retry auth failures: configuration issue.
                _logger.LogWarning(ex, "Failed to publish permission manifest to Access (auth failure, no retry)");
                return;
            }
            catch (Exception ex)
            {
                // Fail-open: don't block service startup if Access is temporarily unavailable.
                if (attempt == maxAttempts)
                {
                    _logger.LogWarning(ex, "Failed to publish permission manifest to Access (fail-open)");
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct);
            }
        }
    }

    private async Task PublishLoopAsync(PermissionManifestPublisherOptions options, CancellationToken ct)
    {
        var intervalSeconds = NormalizeIntervalSeconds(options);
        if (intervalSeconds <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await PublishWithRetryAsync(options, ct);
        }
    }
}
