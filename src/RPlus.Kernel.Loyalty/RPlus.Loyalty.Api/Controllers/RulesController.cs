using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Api.Requests;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty/rules")]
public class RulesController : ControllerBase
{
    private readonly LoyaltyDbContext _dbContext;
    private readonly ILogger<RulesController> _logger;

    public RulesController(LoyaltyDbContext dbContext, ILogger<RulesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoyaltyRule>>> GetRules(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.LoyaltyRules.AsNoTracking().ToListAsync(cancellationToken);
        return Ok(rules);
    }

    [HttpPost]
    public async Task<ActionResult<LoyaltyRule>> CreateRule([FromBody] CreateRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = new LoyaltyRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            EventType = request.EventType,
            Points = request.Points,
            Priority = request.Priority,
            IsActive = request.IsActive,
            Description = request.Description,
            RuleType = request.RuleType,
            RuleConfigJson = request.RuleConfigJson,
            MetadataFilter = request.MetadataFilter.Count > 0 ? JsonSerializer.Serialize(request.MetadataFilter) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.LoyaltyRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.LoyaltyRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule == null)
        {
            return NotFound();
        }

        rule.Name = request.Name;
        rule.EventType = request.EventType;
        rule.Points = request.Points;
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.Description = request.Description;
        rule.RuleType = request.RuleType;
        rule.RuleConfigJson = request.RuleConfigJson;
        rule.MetadataFilter = request.MetadataFilter.Count > 0 ? JsonSerializer.Serialize(request.MetadataFilter) : null;
        rule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult> ChangeStatus(Guid id, [FromBody] ChangeRuleStatusRequest request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.LoyaltyRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule == null)
        {
            return NotFound();
        }

        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
