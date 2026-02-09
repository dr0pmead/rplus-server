using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Application.Services;

public sealed record IntegrationPartnerCacheEntry(
    Guid PartnerId,
    bool IsActive,
    bool IsDiscountPartner,
    decimal? DiscountPartner,
    string AccessLevel,
    IReadOnlyList<string> ProfileFields);

public interface IIntegrationPartnerCache
{
    Task<IntegrationPartnerCacheEntry?> GetAsync(Guid partnerId, CancellationToken cancellationToken);
    Task InvalidateAsync(Guid partnerId, CancellationToken cancellationToken);
    Task SetAsync(IntegrationPartnerCacheEntry entry, CancellationToken cancellationToken);
}
