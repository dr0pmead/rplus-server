using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using RPlusGrpc.Integration.Admin;

namespace RPlus.Access.Api.Services;

public interface IIntegrationAdminClient
{
    Task EnsurePartnerAsync(Guid partnerId, string name, string description, bool isDiscountPartner, CancellationToken cancellationToken);

    Task<IssuedIntegrationApiKey> CreateApiKeyAsync(Guid partnerId, string environment, DateTime? expiresAt, bool requireSignature, CancellationToken cancellationToken);
}

public sealed record IssuedIntegrationApiKey(Guid ApiKeyId, Guid PartnerId, string FullKey, string Status, DateTime CreatedAt, DateTime? ExpiresAt);

public sealed class IntegrationAdminClient : IIntegrationAdminClient
{
    private readonly IntegrationAdminService.IntegrationAdminServiceClient _client;
    private readonly IOptionsMonitor<IntegrationAdminClientOptions> _options;

    public IntegrationAdminClient(
        IntegrationAdminService.IntegrationAdminServiceClient client,
        IOptionsMonitor<IntegrationAdminClientOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task EnsurePartnerAsync(Guid partnerId, string name, string description, bool isDiscountPartner, CancellationToken cancellationToken)
    {
        var metadata = BuildMetadata();
        await _client.EnsurePartnerAsync(
            new EnsurePartnerRequest
            {
                Id = partnerId.ToString(),
                Name = name,
                Description = description ?? string.Empty,
                IsDiscountPartner = isDiscountPartner
            },
            headers: metadata,
            cancellationToken: cancellationToken);
    }

    public async Task<IssuedIntegrationApiKey> CreateApiKeyAsync(Guid partnerId, string environment, DateTime? expiresAt, bool requireSignature, CancellationToken cancellationToken)
    {
        var metadata = BuildMetadata();

        Timestamp? ts = null;
        if (expiresAt.HasValue)
        {
            ts = Timestamp.FromDateTime(expiresAt.Value.ToUniversalTime());
        }

        var response = await _client.CreateApiKeyAsync(
            new CreateApiKeyRequest
            {
                PartnerId = partnerId.ToString(),
                Environment = environment ?? string.Empty,
                ExpiresAt = ts,
                RequireSignature = requireSignature
            },
            headers: metadata,
            cancellationToken: cancellationToken);

        var createdAt = response.CreatedAt?.ToDateTime().ToUniversalTime() ?? DateTime.UtcNow;

        DateTime? responseExpiresAt = null;
        if (response.ExpiresAt != null && response.ExpiresAt.Seconds > 0)
        {
            responseExpiresAt = response.ExpiresAt.ToDateTime().ToUniversalTime();
        }

        return new IssuedIntegrationApiKey(
            Guid.Parse(response.ApiKeyId),
            Guid.Parse(response.PartnerId),
            response.FullKey,
            response.Status,
            createdAt,
            responseExpiresAt);
    }

    private Metadata? BuildMetadata()
    {
        var secret = (_options.CurrentValue.SharedSecret ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(secret))
            return null;

        return new Metadata
        {
            { "x-rplus-service-secret", secret }
        };
    }
}

