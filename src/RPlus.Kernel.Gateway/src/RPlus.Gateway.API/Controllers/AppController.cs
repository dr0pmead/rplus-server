using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RPlus.Gateway.Application.Commands.PublishAppRelease;
using RPlus.Gateway.Application.Contracts.Requests;
using RPlus.Gateway.Application.Contracts.Responses;
using RPlus.Gateway.Application.Queries.GetAppVersion;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Gateway.Api.Controllers;

[ApiController]
[Route("api")]
public class AppController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IConfiguration _configuration;

    public AppController(ISender sender, IConfiguration configuration)
    {
        _sender = sender;
        _configuration = configuration;
    }

    [HttpGet("v1/app/{appName}/version")]
    public async Task<IActionResult> GetVersion(string appName)
    {
        var response = await _sender.Send(new GetAppVersionQuery(appName));
        return response != null 
            ? Ok(response) 
            : NotFound(new { error = "app_not_found" });
    }

    [HttpPost("admin/apps")]
    public async Task<IActionResult> CreateRelease([FromBody] CreateAppReleaseRequest request, [FromHeader(Name = "X-System-Api-Key")] string? apiKey)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "invalid_request" });

            var expectedApiKey = _configuration["SystemApiKey:Key"] ?? _configuration["SYSTEM_API_KEY"];
            if (string.IsNullOrWhiteSpace(expectedApiKey))
                return StatusCode(503, new { error = "service_unavailable" });

            if (string.IsNullOrWhiteSpace(apiKey))
                return Unauthorized(new { error = "unauthorized" });

            if (!FixedTimeEquals(apiKey, expectedApiKey))
                return StatusCode(403, new { error = "forbidden" });

            var id = await _sender.Send(new PublishAppReleaseCommand(request));
            return Created($"/api/v1/app/{request.AppName}/version", new { id });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
