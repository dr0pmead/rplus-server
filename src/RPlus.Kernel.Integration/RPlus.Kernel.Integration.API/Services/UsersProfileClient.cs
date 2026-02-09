using Grpc.Core;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Users;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IUsersProfileClient
{
    Task<UsersProfileDto?> GetProfileAsync(Guid userId, IReadOnlyCollection<string>? metaKeys, CancellationToken cancellationToken);
}

public sealed record UsersProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PreferredName,
    string Locale,
    string TimeZone,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? AvatarId,
    IReadOnlyDictionary<string, string> MetaJson);

public sealed class UsersProfileClient : IUsersProfileClient
{
    private readonly UsersService.UsersServiceClient _client;
    private readonly ILogger<UsersProfileClient> _logger;

    public UsersProfileClient(UsersService.UsersServiceClient client, ILogger<UsersProfileClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<UsersProfileDto?> GetProfileAsync(Guid userId, IReadOnlyCollection<string>? metaKeys, CancellationToken cancellationToken)
    {
        try
        {
            if (metaKeys != null && metaKeys.Count > 0)
            {
                var profile = await _client.GetProfileWithMetaAsync(
                    new GetProfileWithMetaRequest
                    {
                        UserId = userId.ToString(),
                        MetaKeys = { metaKeys }
                    },
                    cancellationToken: cancellationToken);

                return Map(profile.Profile, profile.MetaJson);
            }

            var basic = await _client.GetProfileAsync(
                new GetProfileRequest { UserId = userId.ToString() },
                cancellationToken: cancellationToken);

            return Map(basic, null);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch profile from Users for {UserId}", userId);
            throw;
        }
    }

    private static UsersProfileDto Map(UserProfile profile, IReadOnlyDictionary<string, string>? metaJson)
    {
        return new UsersProfileDto(
            Guid.Parse(profile.Id),
            profile.FirstName,
            profile.LastName,
            string.IsNullOrWhiteSpace(profile.MiddleName) ? null : profile.MiddleName,
            string.IsNullOrWhiteSpace(profile.PreferredName) ? null : profile.PreferredName,
            profile.Locale,
            profile.TimeZone,
            profile.Status,
            profile.CreatedAt.ToDateTime(),
            profile.UpdatedAt.ToDateTime(),
            string.IsNullOrWhiteSpace(profile.AvatarId) ? null : profile.AvatarId,
            metaJson ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
