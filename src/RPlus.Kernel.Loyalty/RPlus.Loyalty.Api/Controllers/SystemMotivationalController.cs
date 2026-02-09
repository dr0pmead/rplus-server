using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RPlusGrpc.Wallet;
using RPlus.Loyalty.Application.Abstractions;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

namespace RPlus.Loyalty.Api.Controllers;

/// <summary>
/// Admin endpoints for motivational points system.
/// Authorization is handled by Gateway - this service trusts all incoming requests.
/// </summary>
[ApiController]
[Route("api/loyalty/system/motivational")]
public sealed class SystemMotivationalController : ControllerBase
{
    private readonly WalletService.WalletServiceClient _walletClient;
    private readonly IMotivationalTierRecalculator _recalculator;
    private readonly IMotivationalTierCatalog _catalog;
    private readonly ILeaderboardService _leaderboard;
    private readonly Microsoft.Extensions.Logging.ILogger<SystemMotivationalController> _logger;
    private readonly string _walletHmacSecret;

    public SystemMotivationalController(
        WalletService.WalletServiceClient walletClient,
        IMotivationalTierRecalculator recalculator,
        IMotivationalTierCatalog catalog,
        ILeaderboardService leaderboard,
        Microsoft.Extensions.Logging.ILogger<SystemMotivationalController> logger,
        IConfiguration configuration)
    {
        _walletClient = walletClient;
        _recalculator = recalculator;
        _catalog = catalog;
        _leaderboard = leaderboard;
        _logger = logger;
        _walletHmacSecret = configuration["Wallet:HmacSecret"] ?? "super-secret-env-key";
    }

    /// <summary>
    /// Run monthly motivational recalculation now.
    /// POST /api/loyalty/system/motivational/run
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunNow(
        [FromQuery] bool force = false,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new MotivationalRecalcRequest(
                Force: force,
                Year: year,
                Month: month);

            var result = await _recalculator.RecalculateAsync(request, ct);

            return Ok(new
            {
                success = result.Success,
                skipped = result.Skipped,
                totalUsers = result.TotalUsers,
                updatedUsers = result.UpdatedUsers,
                tiersHash = result.TiersHash,
                error = result.Error,
                triggeredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RunNow endpoint failed");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Recalculate tier for a single user.
    /// POST /api/loyalty/system/motivational/recalculate/{userId}
    /// </summary>
    [HttpPost("recalculate/{userId:guid}")]
    public async Task<IActionResult> RecalculateUser(
        Guid userId,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var y = year ?? now.AddMonths(-1).Year;
            var m = month ?? now.AddMonths(-1).Month;

            var result = await _recalculator.RecalculateUserAsync(userId, y, m, ct);

            return Ok(new
            {
                success = result.Success,
                userId,
                year = y,
                month = m,
                tierKey = result.TierKey,
                discount = result.Discount,
                monthlyPoints = result.MonthlyPoints,
                updated = result.Updated,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecalculateUser endpoint failed for {UserId}", userId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get configured tiers from Meta.
    /// GET /api/loyalty/system/motivational/tiers
    /// </summary>
    [HttpGet("tiers")]
    public async Task<IActionResult> GetTiers(CancellationToken ct)
    {
        try
        {
            var tiers = await _catalog.GetTiersAsync(ct);

            return Ok(new
            {
                count = tiers.Count,
                tiers = tiers.Select(t => new
                {
                    key = t.Key,
                    title = t.Title,
                    minPoints = t.MinPoints,
                    discount = t.Discount
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTiers endpoint failed");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Accrue points to a user.
    /// POST /api/loyalty/system/motivational/accrue
    /// </summary>
    [HttpPost("accrue")]
    public async Task<IActionResult> AccrueTestPoints(
        [FromBody] AccrueTestPointsRequest request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "UserId is required" });

        if (request.Amount <= 0)
            return BadRequest(new { error = "Amount must be positive" });

        try
        {
            var operationId = request.OperationId ?? Guid.NewGuid().ToString();
            var requestId = Guid.NewGuid().ToString();
            
            // Support backdating for testing motivational system
            DateTimeOffset timestamp;
            if (request.BackdateYear.HasValue && request.BackdateMonth.HasValue)
            {
                // Use the 15th of the backdated month at noon
                timestamp = new DateTimeOffset(
                    request.BackdateYear.Value,
                    request.BackdateMonth.Value,
                    15, 12, 0, 0,
                    TimeSpan.Zero);
                _logger.LogInformation("Backdating accrual to {Year}/{Month}", request.BackdateYear, request.BackdateMonth);
            }
            else
            {
                timestamp = DateTimeOffset.UtcNow;
            }
            
            var timestampMs = timestamp.ToUnixTimeMilliseconds();
            
            var payload = $"{request.UserId}|{request.Amount}|{operationId}|{timestampMs}|{requestId}";
            var signature = ComputeHmacSignature(payload);
            
            var grpcRequest = new AccruePointsRequest
            {
                UserId = request.UserId.ToString(),
                Amount = request.Amount,
                Source = request.Source ?? "system_test",
                OperationId = operationId,
                Description = request.Description ?? "Admin accrual",
                SourceType = request.SourceType ?? "test",
                SourceCategory = request.SourceCategory ?? "admin_test",
                RequestId = requestId,
                Timestamp = Timestamp.FromDateTime(timestamp.UtcDateTime),
                Signature = signature
            };

            var response = await _walletClient.AccruePointsAsync(grpcRequest, cancellationToken: ct);

            var isSuccess = response.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) 
                          || response.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
            
            // If sourceType is "activity", also increment leaderboard score
            if (isSuccess && string.Equals(request.SourceType, "activity", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var year = request.BackdateYear ?? timestamp.Year;
                    var month = request.BackdateMonth ?? timestamp.Month;
                    
                    await _leaderboard.IncrementScoreAsync(request.UserId, request.Amount, year, month, ct);
                    
                    _logger.LogInformation(
                        "Incremented leaderboard score for {UserId}: +{Points} in {Year}/{Month}",
                        request.UserId, request.Amount, year, month);
                }
                catch (Exception lbEx)
                {
                    _logger.LogWarning(lbEx, "Failed to increment leaderboard for {UserId}", request.UserId);
                    // Don't fail the request, wallet accrual was successful
                }
            }

            return Ok(new
            {
                success = isSuccess,
                operationId = response.OperationId,
                balanceAfter = response.BalanceAfter,
                status = response.Status,
                errorCode = response.ErrorCode,
                accruedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AccrueTestPoints endpoint failed for {UserId}", request.UserId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
    
    private string ComputeHmacSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_walletHmacSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>
    /// Get monthly points for a specific user.
    /// GET /api/loyalty/system/motivational/points/{userId}
    /// </summary>
    [HttpGet("points/{userId:guid}")]
    public async Task<IActionResult> GetMonthlyPoints(
        Guid userId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] string? sourceTypes,
        CancellationToken ct)
    {
        var requestYear = year ?? DateTime.UtcNow.Year;
        var requestMonth = month ?? DateTime.UtcNow.Month;

        try
        {
            var grpcRequest = new GetMonthlyPointsRequest
            {
                UserId = userId.ToString(),
                Year = requestYear,
                Month = requestMonth
            };

            if (!string.IsNullOrEmpty(sourceTypes))
            {
                grpcRequest.SourceTypes.AddRange(
                    sourceTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()));
            }

            var response = await _walletClient.GetMonthlyPointsAsync(grpcRequest, cancellationToken: ct);

            return Ok(new
            {
                userId,
                year = requestYear,
                month = requestMonth,
                totalPoints = response.TotalPoints,
                transactionCount = response.TransactionCount,
                success = response.Success,
                error = response.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMonthlyPoints endpoint failed for {UserId}", userId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get transaction history for a user.
    /// GET /api/loyalty/system/motivational/transactions/{userId}
    /// </summary>
    [HttpGet("transactions/{userId:guid}")]
    public async Task<IActionResult> GetTransactions(
        Guid userId,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        try
        {
            var grpcRequest = new GetHistoryRequest
            {
                UserId = userId.ToString(),
                Limit = pageSize,
                Cursor = cursor ?? ""
            };

            var response = await _walletClient.GetHistoryAsync(grpcRequest, cancellationToken: ct);

            return Ok(new
            {
                transactions = response.Items.Select(t => new
                {
                    operationId = t.OperationId,
                    amount = t.Amount,
                    balanceBefore = t.BalanceBefore,
                    balanceAfter = t.BalanceAfter,
                    source = t.Source,
                    status = t.Status,
                    createdAt = t.CreatedAt?.ToDateTime(),
                    processedAt = t.ProcessedAt?.ToDateTime(),
                    description = t.Description
                }),
                nextCursor = response.NextCursor,
                total = response.Items.Count,
                page = 1,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTransactions endpoint failed for {UserId}", userId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public class AccrueTestPointsRequest
{
    public Guid UserId { get; set; }
    public long Amount { get; set; }
    public string? Source { get; set; }
    public string? OperationId { get; set; }
    public string? Description { get; set; }
    public string? SourceType { get; set; }
    public string? SourceCategory { get; set; }
    /// <summary>
    /// Optional: backdate to specific year (for testing motivational system)
    /// </summary>
    public int? BackdateYear { get; set; }
    /// <summary>
    /// Optional: backdate to specific month (for testing motivational system)
    /// </summary>
    public int? BackdateMonth { get; set; }
}
