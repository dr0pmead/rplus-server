using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPlus.Organization.Api.Contracts;
using RPlus.Organization.Api.Services;
using RPlus.SDK.Core.Attributes;

namespace RPlus.Organization.Api.Controllers;

[ApiController]
[Route("api/organization/users")]
[Authorize]
public sealed class UserProfilesController : ControllerBase
{
    private readonly IUserProfileProvider _profiles;

    public UserProfilesController(IUserProfileProvider profiles)
    {
        _profiles = profiles;
    }

    [HttpPost("profiles")]
    [Permission("organization.users.profiles.read", new[] { "WebAdmin" })]
    public async Task<IActionResult> GetProfiles([FromBody] UserProfilesRequest request, CancellationToken ct)
    {
        if (request is null || request.UserIds == null)
            return BadRequest(new { error = "invalid_request" });

        var profiles = await _profiles.GetProfilesAsync(request.UserIds, ct);
        return Ok(profiles);
    }
}

