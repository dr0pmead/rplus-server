using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RPlusGrpc.Access;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Users.Api.Services;

public sealed class UsersPermissionManifestPublisherHostedService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UsersPermissionManifestPublisherHostedService> _logger;

    public UsersPermissionManifestPublisherHostedService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IHostApplicationLifetime lifetime,
        ILogger<UsersPermissionManifestPublisherHostedService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _lifetime = lifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(() => PublishAsync(CancellationToken.None));
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task PublishAsync(CancellationToken ct)
    {
        const int maxAttempts = 10;

        var accessGrpcAddress =
            _configuration["Services:Access:Grpc"]
            ?? $"http://{_configuration["ACCESS_GRPC_HOST"] ?? "rplus-kernel-access"}:{_configuration["ACCESS_GRPC_PORT"] ?? "5003"}";

        var sharedSecret =
            _configuration["Access:PermissionManifest:SharedSecret"]
            ?? _configuration["ACCESS_PERMISSION_MANIFEST_SECRET"];

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false
                };

                using var httpClient = new HttpClient(handler);
                using var channel = GrpcChannel.ForAddress(accessGrpcAddress, new GrpcChannelOptions
                {
                    HttpClient = httpClient
                });

                var client = new AccessService.AccessServiceClient(channel);

                var request = new UpsertPermissionManifestRequest
                {
                    ServiceName = "users",
                    ApplicationId = "users",
                    MarkMissingAsDeprecated = true
                };

                request.Permissions.Add(new PermissionManifestEntry
                {
                    PermissionId = "users.read",
                    Title = "Users: read",
                    Description = "Allows reading users list and user details."
                });

                request.Permissions.Add(new PermissionManifestEntry
                {
                    PermissionId = "users.manage",
                    Title = "Users: manage",
                    Description = "Allows creating and updating users."
                });

                Metadata? headers = null;
                if (!string.IsNullOrWhiteSpace(sharedSecret))
                {
                    headers = new Metadata { { "x-rplus-service-secret", sharedSecret.Trim() } };
                }
                else if (!_environment.IsDevelopment())
                {
                    _logger.LogWarning("Users permission manifest publishing has no SharedSecret configured in non-development environment");
                }

                var response = await client.UpsertPermissionManifestAsync(request, headers, cancellationToken: ct);

                _logger.LogInformation(
                    "Published permission manifest: service=users app=users total={Total} upserted={Upserted} deprecated={Deprecated}",
                    request.Permissions.Count,
                    response.Upserted,
                    response.Deprecated);
                return;
            }
            catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated or StatusCode.PermissionDenied)
            {
                _logger.LogWarning(ex, "Failed to publish users permission manifest to Access (auth failure, no retry)");
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxAttempts)
                {
                    _logger.LogWarning(ex, "Failed to publish users permission manifest to Access (fail-open)");
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct);
            }
        }
    }
}
