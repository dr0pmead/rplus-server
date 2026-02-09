using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Api.Requests;
using RPlus.Loyalty.Api.Responses;
using RPlus.Loyalty.Application.Abstractions;
using RPlus.Loyalty.Application.Handlers;
using RPlus.Loyalty.Domain.Entities;
using System.Text.Json;
using RPlus.Loyalty.Persistence;
using RPlus.SDK.Loyalty.Events;
using RPlus.SDK.Loyalty.Results;
using RPlusGrpc.Wallet;
using RPlus.Loyalty.Infrastructure.Services;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty/profiles")]
public class ProfilesController : ControllerBase
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly WalletService.WalletServiceClient _walletClient;
    private readonly IWalletBalanceCache _balanceCache;
    private readonly ILoyaltyLevelCatalog _levelCatalog;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(
        LoyaltyDbContext dbContext,
        IMediator mediator,
        WalletService.WalletServiceClient walletClient,
        IWalletBalanceCache balanceCache,
        ILoyaltyLevelCatalog levelCatalog,
        ILogger<ProfilesController> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _walletClient = walletClient;
        _balanceCache = balanceCache;
        _levelCatalog = levelCatalog;
        _logger = logger;
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<LoyaltyProfileResponse>> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        var (profile, program) = await EnsureProfilesAsync(userId, cancellationToken);

        var tags = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(program?.TagsJson))
        {
            try
            {
                tags = JsonSerializer.Deserialize<string[]>(program.TagsJson) ?? Array.Empty<string>();
            }
            catch
            {
                tags = Array.Empty<string>();
            }
        }

        // Cache-first balance lookup with gRPC fallback
        decimal pointsBalance = 0;
        
        // 1. Try Redis cache first (~1ms)
        var cachedBalance = await _balanceCache.GetBalanceAsync(userId, cancellationToken);
        if (cachedBalance.HasValue)
        {
            pointsBalance = cachedBalance.Value;
        }
        else
        {
            // 2. Cache miss - fetch from Wallet gRPC and populate cache
            try
            {
                var balanceResponse = await _walletClient.GetBalanceAsync(
                    new GetBalanceRequest { UserId = userId.ToString() },
                    cancellationToken: cancellationToken);
                pointsBalance = balanceResponse.Balance;
                
                // Populate cache for next request
                await _balanceCache.SetBalanceAsync(userId, pointsBalance, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch wallet balance for {UserId}, using local fallback", userId);
                pointsBalance = profile.PointsBalance; // Fallback to local DB
            }
        }

        // v3.0: Get CurrentLevel and TotalLevels for ratio calculation
        var levels = await _levelCatalog.GetLevelsAsync(cancellationToken);
        int totalLevels = levels.Count > 0 ? levels.Count : 1;
        int currentLevel = 1;

        if (program?.Level != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (string.Equals(levels[i].Key, program.Level, StringComparison.OrdinalIgnoreCase))
                {
                    currentLevel = i + 1;
                    break;
                }
            }
        }

        return new LoyaltyProfileResponse
        {
            UserId = profile.UserId,
            PointsBalance = pointsBalance,
            LevelId = profile.LevelId,
            Level = program?.Level,
            CurrentLevel = currentLevel,
            TotalLevels = totalLevels,
            Discount = program?.Discount ?? 0m,
            MotivationDiscount = program?.MotivationDiscount ?? 0m,
            TotalDiscount = program?.TotalDiscount ?? 0m,
            Tags = tags,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }

    [HttpPost("{userId:guid}/events")]
    public async Task<ActionResult<LoyaltyEventProcessResult>> TriggerEvent(Guid userId, [FromBody] ProcessEventRequest request, CancellationToken cancellationToken)
    {
        var trigger = new LoyaltyTriggerEvent
        {
            EventType = request.EventType,
            UserId = userId,
            OperationId = request.OperationId ?? string.Empty,
            Metadata = request.Metadata,
            Source = request.Source,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            Payload = request.Payload
        };

        var result = await _mediator.Send(new ProcessLoyaltyEventCommand(trigger), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private async Task<(LoyaltyProfile Profile, LoyaltyProgramProfile Program)> EnsureProfilesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        var program = await _dbContext.ProgramProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is not null && program is not null)
            return (profile, program);

        if (profile is null)
        {
            profile = LoyaltyProfile.Create(userId);
            _dbContext.Profiles.Add(profile);
        }

        if (program is null)
        {
            program = new LoyaltyProgramProfile
            {
                UserId = userId,
                Level = "base",
                TagsJson = "[]",
                PointsBalance = 0,
                Discount = 0,
                MotivationDiscount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.ProgramProfiles.Add(program);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (profile, program);
    }
}
