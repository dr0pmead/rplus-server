using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RPlus.Loyalty.Infrastructure.Options;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty/registry")]
public class RegistryController : ControllerBase
{
    private readonly IOptionsMonitor<LoyaltyIngressOptions> _ingress;
    private readonly IEventSchemaRegistryReader _registry;
    private readonly IOptionsMonitor<LoyaltyDynamicConsumptionOptions> _dynamic;

    public RegistryController(
        IOptionsMonitor<LoyaltyIngressOptions> ingress,
        IEventSchemaRegistryReader registry,
        IOptionsMonitor<LoyaltyDynamicConsumptionOptions> dynamic)
    {
        _ingress = ingress;
        _registry = registry;
        _dynamic = dynamic;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var current = _ingress.CurrentValue;
        var dynamic = _dynamic.CurrentValue;

        string[] ingressTopics = current.Topics;
        if (dynamic.Enabled)
        {
            try
            {
                var schemas = await _registry.GetAllAsync(ct);
                var schemaTopics = schemas
                    .Where(s => !string.IsNullOrWhiteSpace(s.Topic))
                    .Select(s => s.Topic.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (schemaTopics.Length > 0)
                {
                    ingressTopics = schemaTopics;
                }
            }
            catch
            {
                // fall back to configured ingress topics
            }
        }

        return Ok(new
        {
            ingressTopics,
            ingressMappings = current.Mappings.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    triggerEventType = kv.Value.TriggerEventType,
                    userIdPath = kv.Value.UserIdPath,
                    occurredAtPath = kv.Value.OccurredAtPath,
                    operationIdPath = kv.Value.OperationIdPath,
                    source = kv.Value.Source,
                    metadata = kv.Value.Metadata
                },
                StringComparer.OrdinalIgnoreCase),
            supportedRuleTypes = new[]
            {
                new
                {
                    type = "simple_points",
                    description = "Award fixed points when EventType matches and MetadataFilter matches."
                },
                new
                {
                    type = "streak_days",
                    description = "Award points after N consecutive days. Config: { targetDays:int, cooldownDays:int }."
                },
                new
                {
                    type = "count_within_window",
                    description = "Award points after N occurrences within a window. Config: { threshold:int, windowDays:int, distinctByDay:bool, cooldownDays:int }."
                }
            }
        });
    }
}
