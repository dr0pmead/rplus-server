using Microsoft.Extensions.Logging;
using RPlus.Organization.Api.Contracts;
using RPlusGrpc.Hr;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Organization.Api.Services;

/// <summary>
/// Provides user profiles by fetching FIO/avatar from HR service.
/// Includes in-memory caching with TTL for performance.
/// </summary>
public class UserProfileProvider : IUserProfileProvider
{
    private readonly HrService.HrServiceClient _hrClient;
    private readonly ILogger<UserProfileProvider> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public UserProfileProvider(
        HrService.HrServiceClient hrClient,
        ILogger<UserProfileProvider> logger)
    {
        _hrClient = hrClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, OrganizationUserProfileDto>> GetProfilesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var array = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (array.Length == 0)
            return new Dictionary<Guid, OrganizationUserProfileDto>();

        var now = DateTimeOffset.UtcNow;
        var result = new Dictionary<Guid, OrganizationUserProfileDto>();
        var toFetch = new List<Guid>();

        // Check cache first
        foreach (var id in array)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                result[id] = entry.Profile;
                if (now - entry.UpdatedAt > _ttl)
                    toFetch.Add(id); // Stale, refresh in background
            }
            else
            {
                toFetch.Add(id);
            }
        }

        if (toFetch.Count == 0)
            return result;

        try
        {
            var request = new GetProfilesByUserIdsRequest();
            request.UserIds.AddRange(toFetch.Select(id => id.ToString()));

            var response = await _hrClient.GetProfilesByUserIdsAsync(
                request,
                deadline: null,
                cancellationToken: cancellationToken);

            foreach (var kvp in response.Profiles)
            {
                if (!Guid.TryParse(kvp.Key, out var userId))
                    continue;

                var profile = MapProfile(kvp.Value);
                _cache[userId] = new CacheEntry(profile, now);
                result[userId] = profile;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profiles from HR service");
        }

        return result;
    }

    private static OrganizationUserProfileDto MapProfile(HrUserProfile hrProfile)
    {
        return new OrganizationUserProfileDto(
            Guid.Parse(hrProfile.UserId),
            hrProfile.FirstName,
            hrProfile.LastName,
            string.IsNullOrWhiteSpace(hrProfile.MiddleName) ? null : hrProfile.MiddleName,
            null, // PreferredName - not in HR
            string.IsNullOrWhiteSpace(hrProfile.AvatarId) ? null : hrProfile.AvatarId,
            hrProfile.Status);
    }

    private sealed record CacheEntry(OrganizationUserProfileDto Profile, DateTimeOffset UpdatedAt);
}
