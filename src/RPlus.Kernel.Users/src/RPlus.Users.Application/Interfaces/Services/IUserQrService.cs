using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Users.Application.Interfaces.Services;

public interface IUserQrService
{
    Task<UserQrIssueResult> IssueAsync(Guid userId, string? traceId, CancellationToken ct);
}

public sealed record UserQrIssueResult(string Token, DateTimeOffset ExpiresAt, int TtlSeconds);
