using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Loyalty.Api.Requests;
using RPlus.Loyalty.Api.Responses;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/loyalty/scheduler")]
public sealed class SchedulerController : ControllerBase
{
    private const int MaxPayloadBytes = 64 * 1024;

    private readonly LoyaltyDbContext _db;

    public SchedulerController(LoyaltyDbContext db)
    {
        _db = db;
    }

    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IEnumerable<ScheduledJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ScheduledJobDto>>> GetJobs([FromQuery] string? status, CancellationToken ct)
    {
        var query = _db.ScheduledJobs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(j => j.Status == status);
        }

        var items = await query
            .OrderByDescending(j => j.RunAtUtc)
            .Take(200)
            .ToListAsync(ct);

        return Ok(items.Select(ToDto));
    }

    [HttpPost("jobs")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ScheduledJobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScheduledJobDto>> Create([FromBody] CreateScheduledJobRequest request, CancellationToken ct)
    {
        if (request.RuleId == Guid.Empty || request.UserId == Guid.Empty)
        {
            return BadRequest(new { error = "INVALID_REQUEST" });
        }

        var runAt = request.RunAtUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.RunAtUtc, DateTimeKind.Utc) : request.RunAtUtc.ToUniversalTime();

        var payloadJson = request.Payload?.GetRawText() ?? "{}";
        if (payloadJson.Length > MaxPayloadBytes)
        {
            return BadRequest(new { error = "PAYLOAD_TOO_LARGE" });
        }

        var operationId = string.IsNullOrWhiteSpace(request.OperationId)
            ? ComputeDeterministicOperationId(request.RuleId, request.UserId, runAt, request.EventType)
            : request.OperationId.Trim();

        var job = new LoyaltyScheduledJob
        {
            Id = Guid.NewGuid(),
            RuleId = request.RuleId,
            UserId = request.UserId,
            RunAtUtc = runAt,
            OperationId = operationId,
            EventType = string.IsNullOrWhiteSpace(request.EventType) ? null : request.EventType.Trim(),
            PayloadJson = payloadJson,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.ScheduledJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetJobs), new { id = job.Id }, ToDto(job));
    }

    private static string ComputeDeterministicOperationId(Guid ruleId, Guid userId, DateTime runAtUtc, string? eventType)
    {
        using var sha = SHA256.Create();
        var input = $"{ruleId:N}:{userId:N}:{runAtUtc:O}:{eventType ?? string.Empty}";
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static ScheduledJobDto ToDto(LoyaltyScheduledJob job)
    {
        JsonElement payload;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(job.PayloadJson) ? "{}" : job.PayloadJson);
            payload = doc.RootElement.Clone();
        }
        catch
        {
            using var doc = JsonDocument.Parse("{}");
            payload = doc.RootElement.Clone();
        }

        return new ScheduledJobDto
        {
            Id = job.Id,
            RuleId = job.RuleId,
            UserId = job.UserId,
            RunAtUtc = job.RunAtUtc,
            OperationId = job.OperationId,
            EventType = job.EventType,
            Payload = payload,
            Status = job.Status,
            Attempts = job.Attempts,
            LastError = job.LastError,
            PointsAwarded = job.PointsAwarded,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc
        };
    }
}

