using Microsoft.AspNetCore.Mvc;
using RPlus.Loyalty.Application.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Api.Controllers;

[ApiController]
[Route("api/loyalty/system/tenure")]
public sealed class SystemTenureController : ControllerBase
{
    private readonly ITenureLevelRecalculator _recalculator;

    public SystemTenureController(ITenureLevelRecalculator recalc)
    {
        _recalculator = recalc;
    }

    [HttpPost("run")]
    public async Task<IActionResult> RunNow(CancellationToken ct)
    {
        var result = await _recalculator.RecalculateAsync(new TenureRecalcRequest(Force: true), ct);

        return Ok(new
        {
            success = result.Success,
            skipped = result.Skipped,
            totalUsers = result.TotalUsers,
            updatedUsers = result.UpdatedUsers,
            levelsHash = result.LevelsHash,
            error = result.Error
        });
    }
}
