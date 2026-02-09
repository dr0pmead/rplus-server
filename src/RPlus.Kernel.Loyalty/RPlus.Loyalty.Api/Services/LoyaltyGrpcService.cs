using Grpc.Core;
using RPlusGrpc.Loyalty;
using RPlus.Loyalty.Persistence;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.WellKnownTypes;

namespace RPlus.Loyalty.Api.Services;

public class LoyaltyGrpcService : LoyaltyService.LoyaltyServiceBase
{
    private readonly LoyaltyDbContext _db;
    private readonly ITenureLevelRecalculator _tenureRecalculator;
    private readonly ILogger<LoyaltyGrpcService> _logger;

    public LoyaltyGrpcService(
        LoyaltyDbContext db,
        ITenureLevelRecalculator tenureRecalculator,
        ILogger<LoyaltyGrpcService> logger)
    {
        _db = db;
        _tenureRecalculator = tenureRecalculator;
        _logger = logger;
    }

    public override async Task<CreateProfileResponse> CreateProfile(CreateProfileRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
             throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId"));
        }

        // Check if profile exists
        var exists = await _db.Profiles.AnyAsync(x => x.UserId == userId, context.CancellationToken);
        if (exists)
        {
             var existing = await _db.Profiles.FirstAsync(x => x.UserId == userId, context.CancellationToken);
             return new CreateProfileResponse 
             {
                 Id = existing.Id.ToString(),
                 UserId = existing.UserId.ToString()
             };
        }

        // Use Factory method or handle private setters if Factory is not sufficient
        // Factory Create takes userId and levelId (Guid)
        // Request level is string ("base"), needs conversion or dummy logic.
        // For MVP, assuming "base" -> null or specific Guid?
        // Let's use Guid.Empty if lookup fails, or try to parse if it was a Guid.
        
        Guid? levelId = null;
        if (Guid.TryParse(request.Level, out var parsedLevelId))
        {
            levelId = parsedLevelId;
        }

        var profile = LoyaltyProfile.Create(userId, levelId);
        
        await _db.Profiles.AddAsync(profile, context.CancellationToken);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new CreateProfileResponse
        {
             Id = profile.Id.ToString(),
             UserId = profile.UserId.ToString()
        };
    }

    public override async Task<RecalculateUserTenureResponse> RecalculateUserTenure(
        RecalculateUserTenureRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            return new RecalculateUserTenureResponse
            {
                Success = false,
                Error = "invalid_user_id"
            };
        }

        var result = await _tenureRecalculator.RecalculateUserAsync(userId, context.CancellationToken);

        return new RecalculateUserTenureResponse
        {
            Success = result.Success,
            Level = result.Level ?? string.Empty,
            Discount = (double)result.Discount,
            Updated = result.Updated,
            Error = result.Error ?? string.Empty
        };
    }
}
