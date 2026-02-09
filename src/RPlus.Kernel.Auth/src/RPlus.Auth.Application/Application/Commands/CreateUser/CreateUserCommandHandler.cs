using MediatR;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Application.Security;
using RPlus.Auth.Domain.Entities;
using RPlus.SDK.Auth.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RPlus.Auth.Application.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IAuthDataService _authDataService;
    private readonly ICryptoService _crypto;
    private readonly IPhoneUtil _phoneUtil;
    private readonly IUserAuthEventPublisher _eventPublisher;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IAuthDataService authDataService,
        ICryptoService crypto,
        IPhoneUtil phoneUtil,
        IUserAuthEventPublisher eventPublisher,
        ILogger<CreateUserCommandHandler> logger)
    {
        _authDataService = authDataService;
        _crypto = crypto;
        _phoneUtil = phoneUtil;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (ReservedLogins.IsReserved(request.Login))
        {
            return new CreateUserResult(false, null, "login_reserved");
        }

        string normalized = _phoneUtil.NormalizeToE164(request.Phone);
        string phoneHash = _crypto.HashPhone(normalized);

        // Check for existing user by login or phone
        if (await _authDataService.GetUserByPhoneHashAsync(phoneHash, ct) != null)
        {
             return new CreateUserResult(false, null, "user_already_exists");
        }

        if (await _authDataService.GetUserByIdentifierAsync(request.Login, ct) != null)
        {
             return new CreateUserResult(false, null, "user_already_exists");
        }

        // Create Auth User
        var user = new AuthUserEntity
        {
            Id = Guid.NewGuid(),
            Login = request.Login,
            Email = request.Email,
            PhoneHash = phoneHash,
            PhoneEncrypted = await _crypto.EncryptPhoneAsync(normalized, ct),
            UserType = request.UserType,
            CreatedAt = DateTime.UtcNow,
            SecurityVersion = 1,
            PasswordVersion = 1,
            TenantId = request.TenantId ?? Guid.Empty
        };

        await _authDataService.CreateUserAsync(user, ct);

        // Create Credentials
        byte[] salt = _crypto.GenerateSalt();
        byte[] passwordHash = _crypto.HashPassword(request.Password, salt);

        var credential = new AuthCredentialEntity
        {
            UserId = user.Id,
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            CreatedAt = DateTime.UtcNow,
            ChangedAt = DateTime.UtcNow
        };

        await _authDataService.AddAuthCredentialAsync(credential, ct);

        // Create Known User entry
        var knownUser = new AuthKnownUserEntity
        {
            UserId = user.Id,
            PhoneHash = phoneHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _authDataService.CreateKnownUserAsync(knownUser, ct);

        // Prepare Extended Properties for downstream services (HR, Loyalty, etc.)
        var properties = new Dictionary<string, string>();
        
        if (!string.IsNullOrEmpty(request.Iin)) properties["Iin"] = request.Iin;
        if (request.BirthDate.HasValue) properties["BirthDate"] = request.BirthDate.Value.ToString("O");
        if (request.HireDate.HasValue) properties["HireDate"] = request.HireDate.Value.ToString("O");
        if (request.OrganizationNodeId.HasValue) properties["OrganizationNodeId"] = request.OrganizationNodeId.Value.ToString();
        if (request.DivisionNodeId.HasValue) properties["DivisionNodeId"] = request.DivisionNodeId.Value.ToString();
        if (request.DepartmentNodeId.HasValue) properties["DepartmentNodeId"] = request.DepartmentNodeId.Value.ToString();
        if (request.PositionNodeId.HasValue) properties["PositionNodeId"] = request.PositionNodeId.Value.ToString();
        
        // Flatten custom fields into properties with prefix
        if (request.HrCustomFields != null)
        {
            foreach (var kv in request.HrCustomFields)
            {
                properties[$"CustomField:{kv.Key}"] = kv.Value;
            }
        }

        // Publish Event with Extended Properties
        await _eventPublisher.PublishUserCreatedAsync(
            user, 
            request.FirstName, 
            request.LastName, 
            request.MiddleName, 
            properties, 
            ct
        );

        _logger.LogInformation("Created user {UserId} (Login: {Login}) via CreateUserCommand", user.Id, user.Login);

        return new CreateUserResult(true, user.Id, null);
    }
}
