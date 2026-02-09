using MediatR;
using RPlus.SDK.Auth.Enums;
using System;
using System.Collections.Generic;

namespace RPlus.Auth.Application.Commands.CreateUser;

public record CreateUserCommand(
    string Login,
    string Email,
    string Phone,
    string Password,
    string? FirstName,
    string? LastName,
    string? MiddleName,
    AuthUserType UserType,
    Guid? TenantId,
    // HR Extended Fields
    string? Iin,
    DateTime? BirthDate,
    DateTime? HireDate,
    Guid? OrganizationNodeId,
    Guid? DivisionNodeId,
    Guid? DepartmentNodeId,
    Guid? PositionNodeId,
    Dictionary<string, string>? HrCustomFields
) : IRequest<CreateUserResult>;

public record CreateUserResult(bool Success, Guid? UserId, string? ErrorCode);
