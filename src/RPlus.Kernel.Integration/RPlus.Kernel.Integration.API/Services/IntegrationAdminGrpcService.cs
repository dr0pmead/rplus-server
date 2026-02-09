using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Persistence;
using RPlusGrpc.Integration.Admin;

namespace RPlus.Kernel.Integration.Api.Services;

public sealed class IntegrationAdminGrpcService : IntegrationAdminService.IntegrationAdminServiceBase
{
    private readonly IntegrationDbContext _db;
    private readonly RPlus.Kernel.Integration.Infrastructure.Services.IntegrationAdminService _admin;
    private readonly IOptionsMonitor<IntegrationAdminGrpcOptions> _options;
    private readonly ILogger<IntegrationAdminGrpcService> _logger;

    public IntegrationAdminGrpcService(
        IntegrationDbContext db,
        RPlus.Kernel.Integration.Infrastructure.Services.IntegrationAdminService admin,
        IOptionsMonitor<IntegrationAdminGrpcOptions> options,
        ILogger<IntegrationAdminGrpcService> logger)
    {
        _db = db;
        _admin = admin;
        _options = options;
        _logger = logger;
    }

    public override async Task<ListPartnersResponse> ListPartners(ListPartnersRequest request, ServerCallContext context)
    {
        EnsureAuthorizedOrThrow(context);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        if (pageSize > 100)
            pageSize = 100;

        var query = _db.Partners.AsNoTracking().Where(p => p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }

        var total = await query.CountAsync(context.CancellationToken);

        var partners = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var partnerIds = partners.Select(p => p.Id).ToArray();
        var keyCounts = await _db.ApiKeys.AsNoTracking()
            .Where(k => k.PartnerId.HasValue && partnerIds.Contains(k.PartnerId.Value))
            .GroupBy(k => k.PartnerId!.Value)
            .Select(g => new { PartnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PartnerId, x => x.Count, context.CancellationToken);

        var response = new ListPartnersResponse { TotalCount = total };
        response.Items.AddRange(partners.Select(p => new PartnerDto
        {
            Id = p.Id.ToString(),
            Name = p.Name,
            Description = p.Description ?? string.Empty,
            IsDiscountPartner = p.IsDiscountPartner,
            IsActive = p.IsActive,
            CreatedAt = Timestamp.FromDateTime(p.CreatedAt.ToUniversalTime()),
            ApiKeyCount = keyCounts.TryGetValue(p.Id, out var count) ? count : 0
        }));

        return response;
    }

    public override async Task<PartnerDto> GetPartner(GetPartnerRequest request, ServerCallContext context)
    {
        EnsureAuthorizedOrThrow(context);

        if (!Guid.TryParse(request.Id, out var partnerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_partner_id"));

        var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId && p.DeletedAt == null, context.CancellationToken);
        if (partner is null)
            throw new RpcException(new Status(StatusCode.NotFound, "partner_not_found"));

        var apiKeyCount = await _db.ApiKeys.AsNoTracking().CountAsync(k => k.PartnerId == partnerId, context.CancellationToken);

        return new PartnerDto
        {
            Id = partner.Id.ToString(),
            Name = partner.Name,
            Description = partner.Description ?? string.Empty,
            IsDiscountPartner = partner.IsDiscountPartner,
            IsActive = partner.IsActive,
            CreatedAt = Timestamp.FromDateTime(partner.CreatedAt.ToUniversalTime()),
            ApiKeyCount = apiKeyCount
        };
    }

    public override async Task<PartnerDto> EnsurePartner(EnsurePartnerRequest request, ServerCallContext context)
    {
        EnsureAuthorizedOrThrow(context);

        Guid? desiredId = null;
        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            if (!Guid.TryParse(request.Id, out var parsed))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_partner_id"));
            desiredId = parsed;
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_partner_name"));

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length > 512)
            description = description.Substring(0, 512);

        IntegrationPartner? partner = null;

        if (desiredId.HasValue)
        {
            partner = await _db.Partners.FirstOrDefaultAsync(p => p.Id == desiredId.Value, context.CancellationToken);
            if (partner != null)
            {
                partner.Update(name, description, request.IsDiscountPartner);
                partner.Activate();
                await _db.SaveChangesAsync(context.CancellationToken);
            }
        }

        partner ??= new IntegrationPartner(desiredId ?? Guid.NewGuid(), name, description, request.IsDiscountPartner);

        if (desiredId.HasValue)
        {
            var exists = await _db.Partners.AsNoTracking().AnyAsync(p => p.Id == desiredId.Value, context.CancellationToken);
            if (!exists)
            {
                _db.Partners.Add(partner);
                await _db.SaveChangesAsync(context.CancellationToken);
            }
        }
        else
        {
            _db.Partners.Add(partner);
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        return new PartnerDto
        {
            Id = partner.Id.ToString(),
            Name = partner.Name,
            Description = partner.Description ?? string.Empty,
            IsDiscountPartner = partner.IsDiscountPartner,
            IsActive = partner.IsActive,
            CreatedAt = Timestamp.FromDateTime(partner.CreatedAt.ToUniversalTime()),
            ApiKeyCount = 0
        };
    }

    public override async Task<CreateApiKeyResponse> CreateApiKey(CreateApiKeyRequest request, ServerCallContext context)
    {
        EnsureAuthorizedOrThrow(context);

        if (!Guid.TryParse(request.PartnerId, out var partnerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_partner_id"));

        var env = string.IsNullOrWhiteSpace(request.Environment) ? "live" : request.Environment.Trim().ToLowerInvariant();
        if (env is not ("test" or "live"))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_env"));

        DateTime? expiresAt = null;
        if (request.ExpiresAt != null && request.ExpiresAt.Seconds > 0)
        {
            expiresAt = request.ExpiresAt.ToDateTime().ToUniversalTime();
        }

        try
        {
            var (apiKey, apiKeyRaw, hmacSecret) = await _admin.CreateApiKeyAsync(
                partnerId,
                env,
                Array.Empty<string>(),
                new Dictionary<string, int>(),
                expiresAt,
                request.RequireSignature,
                context.CancellationToken);

            return new CreateApiKeyResponse
            {
                ApiKeyId = apiKey.Id.ToString(),
                PartnerId = partnerId.ToString(),
                FullKey = $"{apiKey.Prefix}{apiKeyRaw}",
                HmacSecret = hmacSecret,
                Status = apiKey.Status,
                CreatedAt = Timestamp.FromDateTime(apiKey.CreatedAt.ToUniversalTime()),
                ExpiresAt = apiKey.ExpiresAt.HasValue
                    ? Timestamp.FromDateTime(apiKey.ExpiresAt.Value.ToUniversalTime())
                    : null
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create integration API key for partner {PartnerId}", partnerId);
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create integration API key for partner {PartnerId}", partnerId);
            throw new RpcException(new Status(StatusCode.Internal, "create_api_key_failed"));
        }
    }

    private void EnsureAuthorizedOrThrow(ServerCallContext context)
    {
        var secret = (_options.CurrentValue.SharedSecret ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        var header = context.RequestHeaders.GetValue("x-rplus-service-secret") ?? string.Empty;
        if (!string.Equals(header, secret, StringComparison.Ordinal))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "unauthorized"));
    }
}
