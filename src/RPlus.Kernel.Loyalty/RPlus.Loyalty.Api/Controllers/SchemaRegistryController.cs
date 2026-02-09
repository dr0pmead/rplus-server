using Microsoft.AspNetCore.Mvc;
using RPlus.SDK.Infrastructure.SchemaRegistry;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty/registry")]
public class SchemaRegistryController : ControllerBase
{
    private readonly IEventSchemaRegistryReader _reader;

    public SchemaRegistryController(IEventSchemaRegistryReader reader)
    {
        _reader = reader;
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(CancellationToken ct)
    {
        var items = await _reader.GetAllAsync(ct);
        var filtered = items
            .Where(item => item.Tags != null &&
                           item.Tags.Any(tag => string.Equals(tag, "loyalty", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Ok(filtered);
    }
}
