using Microsoft.AspNetCore.Http;
using RPlus.HR.Application.Interfaces;
using System.Security.Claims;

namespace RPlus.HR.Api.Services;

public sealed class HttpContextHrActorContext : IHrActorContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextHrActorContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? ActorUserId
    {
        get
        {
            var sub = GetUserId(_accessor.HttpContext?.User);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string ActorType
    {
        get
        {
            var sub = GetUserId(_accessor.HttpContext?.User);
            if (string.IsNullOrWhiteSpace(sub))
                return "unknown";

            if (sub.StartsWith("service:", StringComparison.OrdinalIgnoreCase))
                return "service";

            return Guid.TryParse(sub, out _) ? "user" : "unknown";
        }
    }

    public string? ActorService
    {
        get
        {
            var sub = GetUserId(_accessor.HttpContext?.User);
            if (string.IsNullOrWhiteSpace(sub) || !sub.StartsWith("service:", StringComparison.OrdinalIgnoreCase))
                return null;

            return sub;
        }
    }

    private static string? GetUserId(ClaimsPrincipal? principal) =>
        principal?.FindFirstValue("sub")
        ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal?.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
}

