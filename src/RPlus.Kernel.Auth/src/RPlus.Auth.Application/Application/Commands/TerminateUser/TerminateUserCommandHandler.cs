using MediatR;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Auth.Application.Commands.TerminateUser;

/// <summary>
/// Handles employee termination - blocks the user, revokes all sessions, and publishes event.
/// </summary>
public class TerminateUserCommandHandler : IRequestHandler<TerminateUserCommand, TerminateUserResult>
{
    private readonly IAuthDataService _authDataService;
    private readonly IUserAuthEventPublisher _eventPublisher;
    private readonly ILogger<TerminateUserCommandHandler> _logger;

    public TerminateUserCommandHandler(
        IAuthDataService authDataService,
        IUserAuthEventPublisher eventPublisher,
        ILogger<TerminateUserCommandHandler> logger)
    {
        _authDataService = authDataService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<TerminateUserResult> Handle(TerminateUserCommand request, CancellationToken ct)
    {
        var user = await _authDataService.GetUserByIdAsync(request.UserId, ct);
        if (user == null)
        {
            return new TerminateUserResult(false, "user_not_found");
        }

        if (user.IsSystem)
        {
            return new TerminateUserResult(false, "cannot_terminate_system_user");
        }

        if (user.IsBlocked && user.BlockReason == "terminated")
        {
            return new TerminateUserResult(false, "already_terminated");
        }

        // Block the user with termination reason
        user.IsBlocked = true;
        user.BlockReason = "terminated";
        user.BlockedAt = DateTime.UtcNow;
        user.SecurityVersion += 1; // Invalidate all existing tokens

        await _authDataService.UpdateUserAsync(user, ct);

        // Update Known User to inactive
        var knownUser = await _authDataService.GetKnownUserByIdAsync(request.UserId, ct);
        if (knownUser != null)
        {
            knownUser.IsActive = false;
            knownUser.UpdatedAt = DateTime.UtcNow;
            await _authDataService.UpdateKnownUserAsync(knownUser, ct);
        }

        // Revoke all active sessions
        await _authDataService.RevokeAllUserSessionsAsync(request.UserId, "terminated", ct);

        // Publish termination event for downstream services (HR, Loyalty, etc.)
        await _eventPublisher.PublishUserTerminatedAsync(
            request.UserId,
            request.Reason ?? "Employee dismissed",
            ct
        );

        _logger.LogInformation(
            "User {UserId} (Login: {Login}) terminated. Reason: {Reason}",
            user.Id, user.Login, request.Reason ?? "Employee dismissed"
        );

        return new TerminateUserResult(true, null);
    }
}
