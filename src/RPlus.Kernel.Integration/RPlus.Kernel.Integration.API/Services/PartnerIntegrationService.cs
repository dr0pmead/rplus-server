using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Integration.Api.Controllers;
using RPlus.Kernel.Integration.Api.Models.Partners;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Application.Services;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Contracts.Events;
using RPlus.SDK.Eventing.Abstractions;

namespace RPlus.Kernel.Integration.Api.Services;

/// <summary>
/// Partner integration service implementing Double Entry bookkeeping.
/// Handles Intent → Commit flow with full idempotency and atomic transactions.
/// </summary>
public sealed class PartnerIntegrationService : IPartnerIntegrationService
{
    private readonly IIntegrationDbContext _db;
    private readonly IUserTokenResolver _tokenResolver;
    private readonly IDiscountCalculator _discountCalculator;
    private readonly IScanProfileAggregator _profileAggregator;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PartnerIntegrationService> _logger;

    // TTL for scan intent (24 hours by default)
    private static readonly TimeSpan ScanTtl = TimeSpan.FromHours(24);

    public PartnerIntegrationService(
        IIntegrationDbContext db,
        IUserTokenResolver tokenResolver,
        IDiscountCalculator discountCalculator,
        IScanProfileAggregator profileAggregator,
        IEventPublisher eventPublisher,
        ILogger<PartnerIntegrationService> logger)
    {
        _db = db;
        _tokenResolver = tokenResolver;
        _discountCalculator = discountCalculator;
        _profileAggregator = profileAggregator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PartnerScanResponse> ProcessScanAsync(
        Guid partnerId,
        string idempotencyKey,
        PartnerScanRequest request,
        CancellationToken ct = default)
    {
        // ========== Step 1: Idempotency Check (Fast Path) ==========
        var existingScan = await _db.PartnerScans
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PartnerId == partnerId && s.IdempotencyKey == idempotencyKey, ct);

        if (existingScan is not null)
        {
            _logger.LogInformation("Idempotent scan request detected: {ScanId}", existingScan.ScanId);
            return MapToResponse(existingScan);
        }

        // ========== Step 2: Token Resolution (QR or OTP) ==========
        // Use ResolvedToken which handles both qrToken and otpCode
        var token = request.ResolvedToken;
        var tokenResult = await _tokenResolver.ResolveAsync(token, ct);
        if (!tokenResult.Success)
        {
            throw new InvalidOperationException($"Token validation failed: {tokenResult.Error}");
        }

        var userId = tokenResult.UserId;
        var scanMethod = tokenResult.Type == TokenType.ShortCode ? "otp" : "qr";

        // ========== Step 3: Fetch User Profile ==========
        var profile = await _profileAggregator.FetchAndCacheAsync(userId, ct)
            ?? throw new InvalidOperationException("User profile not found");

        // ========== Step 4: Calculate Discounts (v3.0) ==========
        var partner = await _db.Partners
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partnerId, ct)
            ?? throw new InvalidOperationException("Partner not found");

        var partnerConfig = new PartnerDiscountConfig(
            partner.DiscountStrategy ?? "dynamic_level",
            partner.PartnerCategory ?? "retail",
            partner.MaxDiscount,
            null, // FixedDiscount
            partner.HappyHoursConfigJson);

        var userProfile = new CachedUserProfile(
            profile.CurrentLevel,
            profile.TotalLevels,
            profile.RPlusDiscount);

        var discountResult = _discountCalculator.Calculate(userProfile, partnerConfig);

        // ========== Step 5: Create Scan Intent ==========
        var scan = new PartnerScan
        {
            ScanId = Guid.NewGuid(),
            PartnerId = partnerId,
            TerminalId = request.TerminalId ?? string.Empty,
            CashierId = request.CashierId,
            OrderId = request.OrderId,
            OrderSumPredicted = request.OrderSum,
            UserId = userId,
            ScanMethod = scanMethod,
            PredictedUserDiscount = discountResult.RPlusDiscount,
            PredictedPartnerDiscount = discountResult.PartnerDiscount,
            Status = PartnerScanStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ScanTtl),
            IdempotencyKey = idempotencyKey
        };

        // ========== Step 6: Save with Idempotency Handling ==========
        try
        {
            _db.PartnerScans.Add(scan);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created scan intent: ScanId={ScanId}, UserId={UserId}, PartnerId={PartnerId}",
                scan.ScanId, userId, partnerId);

            return MapToResponse(scan, profile, discountResult);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent request won the race - return their result
            _logger.LogWarning("Concurrent scan detected, returning existing: {IdempotencyKey}", idempotencyKey);

            var concurrentScan = await _db.PartnerScans
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.PartnerId == partnerId && s.IdempotencyKey == idempotencyKey, ct)
                ?? throw new InvalidOperationException("Race condition: scan not found after conflict");

            return MapToResponse(concurrentScan);
        }
    }

    /// <inheritdoc />
    public async Task<PartnerCommitResponse> ProcessCommitAsync(
        Guid partnerId,
        PartnerCommitRequest request,
        CancellationToken ct = default)
    {
        // ========== Step 1: Find Scan Intent ==========
        var scan = await _db.PartnerScans
            .FirstOrDefaultAsync(s => s.ScanId == request.ScanId, ct);

        if (scan is null)
        {
            throw new KeyNotFoundException($"Scan not found: {request.ScanId}");
        }

        // ========== Step 2: Anti-Fraud: Verify OrderId ==========
        if (scan.OrderId != request.OrderId)
        {
            _logger.LogWarning(
                "OrderId mismatch! ScanId={ScanId}, Expected={Expected}, Got={Got}",
                request.ScanId, scan.OrderId, request.OrderId);
            throw new InvalidOperationException("OrderId mismatch - potential fraud attempt");
        }

        // ========== Step 3: Idempotency Check ==========
        if (scan.Status == PartnerScanStatus.Committed)
        {
            var existingCommit = await _db.PartnerCommits
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ScanId == request.ScanId, ct);

            if (existingCommit is not null)
            {
                _logger.LogInformation("Idempotent commit request: {CommitId}", existingCommit.CommitId);
                return new PartnerCommitResponse
                {
                    CommitId = existingCommit.CommitId,
                    WasDuplicate = true
                };
            }
        }

        // ========== Step 4: Validate Status ==========
        if (scan.Status == PartnerScanStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot commit cancelled scan");
        }

        // ========== Step 5: Atomic Transaction ==========
        await using var transaction = await ((DbContext)_db).Database.BeginTransactionAsync(ct);

        try
        {
            // Update scan status
            scan.Status = PartnerScanStatus.Committed;
            scan.CommittedAt = DateTime.UtcNow;

            // Create commit record with all fields
            var commit = new PartnerCommit
            {
                CommitId = Guid.NewGuid(),
                ScanId = scan.ScanId,
                Terminal = request.Terminal,
                FinalOrderTotal = request.FinalOrderTotal,
                FinalOrderTotalBeforeDiscounts = request.FinalOrderTotalBeforeDiscounts,
                FinalUserDiscount = request.FinalUserDiscount,
                FinalPartnerDiscount = request.FinalPartnerDiscount,
                UserDiscountAmount = request.UserDiscountAmount,
                PartnerDiscountAmount = request.PartnerDiscountAmount,
                QrDiscountTypeId = request.QrDiscountTypeId,
                QrDiscountPercent = request.QrDiscountPercent,
                QrDiscountSum = request.QrDiscountSum,
                ChequeNumber = request.Cheque?.ChequeNumber,
                FiscalId = request.Cheque?.FiscalId,
                ClosedAt = request.ClosedAt,
                ItemsJson = request.Items is not null ? JsonSerializer.Serialize(request.Items) : null,
                PaymentsJson = request.Payments is not null ? JsonSerializer.Serialize(request.Payments) : null,
                DiscountsJson = request.Discounts is not null ? JsonSerializer.Serialize(request.Discounts) : null,
                WalletProcessed = false, // Will be processed by background job
                CreatedAt = DateTime.UtcNow,
                IdempotencyKey = scan.ScanId.ToString() // Same as ScanId for commits
            };

            _db.PartnerCommits.Add(commit);
            await _db.SaveChangesAsync(ct);

            // Publish event for Wallet processing (via Transactional Outbox)
            var orderEvent = new PartnerOrderCommittedEvent
            {
                CommitId = commit.CommitId,
                ScanId = scan.ScanId,
                PartnerId = scan.PartnerId,
                UserId = scan.UserId,
                OrderTotal = commit.FinalOrderTotal,
                UserDiscountAmount = commit.UserDiscountAmount ?? commit.FinalUserDiscount,
                PartnerDiscountAmount = commit.PartnerDiscountAmount ?? commit.FinalPartnerDiscount,
                CommittedAt = scan.CommittedAt ?? DateTime.UtcNow,
                TerminalId = scan.TerminalId,
                FiscalId = commit.FiscalId
            };

            await _eventPublisher.PublishAsync(
                orderEvent,
                "partner.order.committed",
                commit.CommitId.ToString(),
                ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Committed order: CommitId={CommitId}, ScanId={ScanId}, Total={Total}",
                commit.CommitId, scan.ScanId, request.FinalOrderTotal);

            return new PartnerCommitResponse
            {
                CommitId = commit.CommitId,
                WasDuplicate = false
            };
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent commit - return existing
            await transaction.RollbackAsync(ct);

            var existingCommit = await _db.PartnerCommits
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ScanId == request.ScanId, ct)
                ?? throw new InvalidOperationException("Race condition: commit not found after conflict");

            return new PartnerCommitResponse
            {
                CommitId = existingCommit.CommitId,
                WasDuplicate = true
            };
        }
    }

    /// <inheritdoc />
    public async Task ProcessCancelAsync(
        Guid partnerId,
        PartnerCancelRequest request,
        CancellationToken ct = default)
    {
        var scan = await _db.PartnerScans
            .FirstOrDefaultAsync(s => s.ScanId == request.ScanId && s.PartnerId == partnerId, ct);

        if (scan is null)
        {
            throw new KeyNotFoundException($"Scan not found: {request.ScanId}");
        }

        // Already cancelled — idempotent
        if (scan.Status == PartnerScanStatus.Cancelled)
        {
            _logger.LogInformation("Scan already cancelled: {ScanId}", request.ScanId);
            return;
        }

        // Cannot cancel a committed scan
        if (scan.Status == PartnerScanStatus.Committed)
        {
            throw new InvalidOperationException($"Cannot cancel committed scan: {request.ScanId}");
        }

        scan.Status = PartnerScanStatus.Cancelled;
        scan.CancelledAt = DateTime.UtcNow;
        scan.CancelReason = request.Reason;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cancelled scan: ScanId={ScanId}, Reason={Reason}",
            request.ScanId, request.Reason ?? "none");
    }

    /// <inheritdoc />
    public async Task ProcessEventAsync(
        Guid partnerId,
        string idempotencyKey,
        PartnerEventRequest request,
        CancellationToken ct = default)
    {
        // Best-effort telemetry storage — log and store
        _logger.LogInformation(
            "Partner event: Type={Type}, EventId={EventId}, PartnerId={PartnerId}, ScanId={ScanId}",
            request.Type, request.EventId, partnerId, request.ScanId);

        // Store as audit log entry
        try
        {
            var auditLog = new Domain.Entities.IntegrationAuditLog
            {
                Id = Guid.NewGuid(),
                PartnerId = partnerId,
                Action = $"plugin_event:{request.Type}",
                Details = JsonSerializer.Serialize(new
                {
                    request.EventId,
                    request.At,
                    request.Type,
                    request.ScanId,
                    request.OrderId,
                    request.TerminalId,
                    request.Details
                }),
                CreatedAt = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow,
                RequestMethod = "POST",
                RequestPath = "/api/partners/events",
                TargetService = "integration",
                StatusCode = 200,
                DurationMs = 0,
                ClientIp = string.Empty
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Events should never fail — log and swallow
            _logger.LogWarning(ex, "Failed to persist partner event: {EventId}", request.EventId);
        }
    }

    // ========== Private Helpers ==========

    private static PartnerScanResponse MapToResponse(PartnerScan scan)
    {
        return new PartnerScanResponse
        {
            ScanId = scan.ScanId,
            ExpiresAt = scan.ExpiresAt,
            User = new PartnerScanUserDto(),
            Discounts = new PartnerScanDiscountsDto
            {
                RPlusAmount = scan.PredictedUserDiscount,
                PartnerAmount = scan.PredictedPartnerDiscount,
                TotalPercent = scan.PredictedUserDiscount + scan.PredictedPartnerDiscount
            }
        };
    }

    private static PartnerScanResponse MapToResponse(
        PartnerScan scan,
        ScanProfile profile,
        DiscountResult discounts)
    {
        return new PartnerScanResponse
        {
            ScanId = scan.ScanId,
            ExpiresAt = scan.ExpiresAt,
            User = new PartnerScanUserDto
            {
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                AvatarUrl = profile.AvatarUrl,
                CurrentLevel = profile.CurrentLevel,
                TotalLevels = profile.TotalLevels
            },
            Discounts = new PartnerScanDiscountsDto
            {
                RPlusAmount = discounts.RPlusDiscount,
                PartnerAmount = discounts.PartnerDiscount,
                TotalPercent = discounts.TotalDiscount
            }
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique violation code: 23505
        return ex.InnerException?.Message.Contains("23505") == true
            || ex.InnerException?.Message.Contains("duplicate key") == true
            || ex.InnerException?.Message.Contains("unique constraint") == true;
    }
}
