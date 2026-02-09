using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Loyalty.Api.Requests;
using RPlus.Loyalty.Api.Responses;
using RPlus.Loyalty.Application.Graph;
using RPlus.Loyalty.Domain.Entities;
using RPlus.Loyalty.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/loyalty/graph-rules")]
public sealed class GraphRulesController : ControllerBase
{
    private const int MaxGraphBytes = 128 * 1024;

    private readonly LoyaltyDbContext _db;
    private readonly ILogger<GraphRulesController> _logger;
    private readonly LoyaltyGraphSchemaValidator _validator;

    public GraphRulesController(
        LoyaltyDbContext db,
        ILogger<GraphRulesController> logger,
        LoyaltyGraphSchemaValidator validator)
    {
        _db = db;
        _logger = logger;
        _validator = validator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GraphRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GraphRuleDto>>> Get(CancellationToken ct)
    {
        var items = await _db.GraphRules.AsNoTracking().ToListAsync(ct);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GraphRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GraphRuleDto>> GetById(Guid id, CancellationToken ct)
    {
        var rule = await _db.GraphRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule == null)
        {
            return NotFound();
        }

        return Ok(ToDto(rule));
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GraphRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GraphRuleDto>> Create([FromBody] CreateGraphRuleRequest request, CancellationToken ct)
    {
        if (request.IsSystem && string.IsNullOrWhiteSpace(request.SystemKey))
        {
            return BadRequest(new { error = "SYSTEM_KEY_REQUIRED" });
        }

        if (request.IsSystem)
        {
            var existing = await _db.GraphRules.AsNoTracking()
                .AnyAsync(r => r.SystemKey == request.SystemKey, ct);
            if (existing)
            {
                return BadRequest(new { error = "SYSTEM_KEY_EXISTS" });
            }
        }

        var graphJson = request.Graph.GetRawText();
        if (graphJson.Length > MaxGraphBytes)
        {
            return BadRequest(new { error = "GRAPH_TOO_LARGE" });
        }
        var validation = await _validator.ValidateAsync(graphJson, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = "INVALID_GRAPH", details = validation.Errors });
        }

        var rule = new LoyaltyGraphRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Topic = request.Topic,
            Priority = request.Priority,
            IsActive = request.IsActive,
            MaxExecutions = NormalizeMaxExecutions(request.MaxExecutions),
            GraphJson = graphJson,
            VariablesJson = request.Variables?.GetRawText() ?? "{}",
            IsSystem = request.IsSystem,
            SystemKey = string.IsNullOrWhiteSpace(request.SystemKey) ? null : request.SystemKey.Trim(),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (rule.MaxExecutions.HasValue && rule.ExecutionsCount >= rule.MaxExecutions.Value)
        {
            rule.IsActive = false;
        }

        _db.GraphRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, ToDto(rule));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGraphRuleRequest request, CancellationToken ct)
    {
        var rule = await _db.GraphRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule == null)
        {
            return NotFound();
        }

        if (rule.IsSystem)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "SYSTEM_RULE_LOCKED" });
        }

        var graphJson = request.Graph.GetRawText();
        if (graphJson.Length > MaxGraphBytes)
        {
            return BadRequest(new { error = "GRAPH_TOO_LARGE" });
        }
        var validation = await _validator.ValidateAsync(graphJson, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = "INVALID_GRAPH", details = validation.Errors });
        }

        rule.Name = request.Name;
        rule.Topic = request.Topic;
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.MaxExecutions = NormalizeMaxExecutions(request.MaxExecutions);
        rule.GraphJson = graphJson;
        if (request.Variables.HasValue)
        {
            rule.VariablesJson = request.Variables.Value.GetRawText();
        }
        rule.Description = request.Description;
        rule.UpdatedAt = DateTime.UtcNow;

        if (rule.MaxExecutions.HasValue && rule.ExecutionsCount >= rule.MaxExecutions.Value)
        {
            rule.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated loyalty graph rule {RuleId}", id);
        return NoContent();
    }

    [HttpPut("{id:guid}/variables")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVariables(Guid id, [FromBody] UpdateGraphRuleVariablesRequest request, CancellationToken ct)
    {
        var rule = await _db.GraphRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule == null)
        {
            return NotFound();
        }

        var variablesJson = request.Variables.GetRawText();
        if (variablesJson.Length > MaxGraphBytes)
        {
            return BadRequest(new { error = "VARIABLES_TOO_LARGE" });
        }

        rule.VariablesJson = variablesJson;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeRuleStatusRequest request, CancellationToken ct)
    {
        var rule = await _db.GraphRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule == null)
        {
            return NotFound();
        }

        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static GraphRuleDto ToDto(LoyaltyGraphRule rule)
    {
        JsonElement graph;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.GraphJson) ? "{}" : rule.GraphJson);
            graph = doc.RootElement.Clone();
        }
        catch
        {
            using var doc = JsonDocument.Parse("{}");
            graph = doc.RootElement.Clone();
        }

        JsonElement variables;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.VariablesJson) ? "{}" : rule.VariablesJson);
            variables = doc.RootElement.Clone();
        }
        catch
        {
            using var doc = JsonDocument.Parse("{}");
            variables = doc.RootElement.Clone();
        }

        return new GraphRuleDto
        {
            Id = rule.Id,
            Name = rule.Name,
            Topic = rule.Topic,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            MaxExecutions = rule.MaxExecutions,
            ExecutionsCount = rule.ExecutionsCount,
            Graph = graph,
            Variables = variables,
            IsSystem = rule.IsSystem,
            SystemKey = rule.SystemKey,
            Description = rule.Description,
            CreatedAt = rule.CreatedAt,
            UpdatedAt = rule.UpdatedAt
        };
    }

    private static int? NormalizeMaxExecutions(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value <= 0 ? null : value.Value;
    }
}
