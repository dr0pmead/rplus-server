using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Users.Domain.Entities;
using RPlus.Users.Infrastructure.Persistence;
using RPlusGrpc.Meta;
using RPlusGrpc.Users;

namespace RPlus.Users.Api.Services;

public class UsersGrpcService : UsersService.UsersServiceBase
{
    private readonly UsersDbContext _db;
    private readonly ILogger<UsersGrpcService> _logger;
    private readonly MetaService.MetaServiceClient _metaClient;
    private readonly IOptionsMonitor<UsersMetaClientOptions> _metaOptions;

    public UsersGrpcService(
        UsersDbContext db,
        ILogger<UsersGrpcService> logger,
        MetaService.MetaServiceClient metaClient,
        IOptionsMonitor<UsersMetaClientOptions> metaOptions)
    {
        _db = db;
        _logger = logger;
        _metaClient = metaClient;
        _metaOptions = metaOptions;
    }

    public override async Task<UserProfile> GetProfile(
        GetProfileRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out Guid userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId format"));

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, context.CancellationToken);

        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return MapToDto(user);
    }

    public override async Task<UserProfileWithMeta> GetProfileWithMeta(
        GetProfileWithMetaRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out Guid userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId format"));

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, context.CancellationToken);

        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var response = new UserProfileWithMeta
        {
            Profile = MapToDto(user)
        };

        var keys = request.MetaKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
            return response;

        try
        {
            var metadata = BuildMetaHeaders();
            var metaResult = await _metaClient.GetEntityFieldValuesAsync(
                new GetEntityFieldValuesRequest
                {
                    EntityTypeKey = "user",
                    SubjectId = userId.ToString(),
                    FieldKeys = { keys }
                },
                metadata,
                cancellationToken: context.CancellationToken);

            foreach (var value in metaResult.Values)
            {
                if (!string.IsNullOrWhiteSpace(value.Key))
                    response.MetaJson[value.Key] = value.ValueJson ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load user meta values for {UserId}", userId);
        }

        return response;
    }

    public override async Task<UsersList> GetUsersByIds(
        GetUsersByIdsRequest request,
        ServerCallContext context)
    {
        var ids = request.UserIds
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        if (!ids.Any())
            return new UsersList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(context.CancellationToken);

        var response = new UsersList();
        response.Users.AddRange(users.Select(MapToDto));
        return response;
    }

    // FIO fields removed - now managed by HR module
    public override async Task<UserProfile> UpdateProfile(
        UpdateProfileRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out Guid userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId format"));

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, context.CancellationToken);
        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        user.UpdateProfile(
            request.HasPreferredName ? request.PreferredName : user.PreferredName,
            request.HasLocale ? request.Locale : user.Locale,
            request.HasTimeZone ? request.TimeZone : user.TimeZone,
            request.HasAvatarId ? request.AvatarId : user.AvatarId,
            DateTime.UtcNow);

        await _db.SaveChangesAsync(context.CancellationToken);
        return MapToDto(user);
    }

    // FIO search removed - search now handled by HR module
    public override async Task<ListUsersResponse> ListUsers(
        ListUsersRequest request,
        ServerCallContext context)
    {
        IQueryable<UserEntity> query = _db.Users.AsNoTracking();

        // Search by PreferredName only (FIO search moved to HR)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(u => u.PreferredName != null && u.PreferredName.ToLower().Contains(term));
        }

        int totalCount = await query.CountAsync(context.CancellationToken);
        int pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;
        int pageSize = request.PageSize > 0 ? request.PageSize : 10;

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListUsersResponse
        {
            TotalCount = totalCount
        };
        response.Users.AddRange(users.Select(MapToDto));

        return response;
    }

    // FIO fields removed from DTO mapping
    private static UserProfile MapToDto(UserEntity user)
    {
        return new UserProfile
        {
            Id = user.Id.ToString(),
            FirstName = "", // FIO removed - now in HR module
            LastName = "",  // FIO removed - now in HR module
            MiddleName = "", // FIO removed - now in HR module
            PreferredName = user.PreferredName ?? "",
            Locale = user.Locale,
            TimeZone = user.TimeZone,
            AvatarId = user.AvatarId ?? "",
            Status = user.Status.ToString(),
            CreatedAt = Timestamp.FromDateTime(user.CreatedAt.ToUniversalTime()),
            UpdatedAt = Timestamp.FromDateTime(user.UpdatedAt.ToUniversalTime())
        };
    }

    private Metadata BuildMetaHeaders()
    {
        var secret = (_metaOptions.CurrentValue.ServiceSecret ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(secret))
            return new Metadata();

        return new Metadata { { "x-rplus-service-secret", secret } };
    }
}
