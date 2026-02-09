using MediatR;
using System;

namespace RPlus.Auth.Application.Commands.TerminateUser;

/// <summary>
/// Command to terminate/dismiss an employee. Sets is_active=false, blocks login, revokes all sessions.
/// </summary>
public record TerminateUserCommand(
    Guid UserId,
    string? Reason
) : IRequest<TerminateUserResult>;

public record TerminateUserResult(bool Success, string? ErrorCode);
