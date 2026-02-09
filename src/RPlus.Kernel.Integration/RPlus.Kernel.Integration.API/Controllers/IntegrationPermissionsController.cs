using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RPlusGrpc.Access;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/permissions")]
public class IntegrationPermissionsController : ControllerBase
{
    private readonly AccessService.AccessServiceClient _accessClient;
    private readonly ILogger<IntegrationPermissionsController> _logger;

    public IntegrationPermissionsController(
        AccessService.AccessServiceClient accessClient,
        ILogger<IntegrationPermissionsController> logger)
    {
        _accessClient = accessClient;
        _logger = logger;
    }

    [HttpGet("all")]
    public async Task<ActionResult<IReadOnlyCollection<PermissionResponse>>> GetAllPermissions(
        CancellationToken cancellationToken)
    {
        try
        {
            var permissions = await _accessClient.GetPermissionsAsync(new Empty(), cancellationToken: cancellationToken);
            var result = permissions.Permissions
                .Select(permission => new PermissionResponse(
                    permission.PermissionId,
                    FormatPermissionName(permission.PermissionId),
                    $"Permission for {permission.PermissionId}",
                    GetPermissionCategory(permission.PermissionId),
                    permission.IsActive))
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch permissions from Access service");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to fetch permissions" });
        }
    }

    [HttpGet("partners/{apiKeyId:guid}/permissions")]
    public async Task<ActionResult<PartnerPermissionsResponse>> GetPartnerPermissions(
        Guid apiKeyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _accessClient.GetIntegrationPermissionsAsync(
                new GetIntegrationPermissionsRequest { ApiKeyId = apiKeyId.ToString() },
                cancellationToken: cancellationToken);

            var partnerPermissions = new PartnerPermissionsResponse(apiKeyId, response.Permissions.ToList());
            return Ok(partnerPermissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch partner permissions for {PartnerId}", apiKeyId);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to fetch partner permissions" });
        }
    }

    [HttpPut("partners/{apiKeyId:guid}/permissions")]
    public async Task<IActionResult> UpdatePartnerPermissions(
        Guid apiKeyId,
        [FromBody] UpdatePartnerPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.PermissionIds == null)
        {
            return BadRequest(new { error = "PermissionIds are required" });
        }

        try
        {
            var current = await _accessClient.GetIntegrationPermissionsAsync(
                new GetIntegrationPermissionsRequest { ApiKeyId = apiKeyId.ToString() },
                cancellationToken: cancellationToken);

            var desired = request.PermissionIds
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList();

            var currentSet = new HashSet<string>(current.Permissions, StringComparer.OrdinalIgnoreCase);
            var desiredSet = new HashSet<string>(desired, StringComparer.OrdinalIgnoreCase);

            var toGrant = desiredSet.Except(currentSet, StringComparer.OrdinalIgnoreCase).ToList();
            var toRevoke = currentSet.Except(desiredSet, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var permissionId in toGrant)
            {
                await _accessClient.GrantIntegrationPermissionAsync(
                    new GrantIntegrationPermissionRequest
                    {
                        ApiKeyId = apiKeyId.ToString(),
                        PermissionId = permissionId
                    },
                    cancellationToken: cancellationToken);
            }

            foreach (var permissionId in toRevoke)
            {
                await _accessClient.RevokeIntegrationPermissionAsync(
                    new RevokeIntegrationPermissionRequest
                    {
                        ApiKeyId = apiKeyId.ToString(),
                        PermissionId = permissionId
                    },
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation(
                "Updated permissions for partner {PartnerId}: granted {GrantCount}, revoked {RevokeCount}",
                apiKeyId,
                toGrant.Count,
                toRevoke.Count);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update partner permissions for {PartnerId}", apiKeyId);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Failed to update partner permissions" });
        }
    }

    private static string FormatPermissionName(string permissionId)
    {
        var segments = permissionId.Split('.');
        if (segments.Length != 2)
        {
            return permissionId;
        }

        var area = segments[0] switch
        {
            "organization" => "организации",
            "users" => "пользователей",
            "integration" => "интеграций",
            "access" => "доступа",
            "audit" => "аудита",
            "system" => "системы",
            _ => segments[0]
        };

        var action = segments[1] switch
        {
            "view" => "Просмотр",
            "create" => "Создание",
            "update" => "Редактирование",
            "delete" => "Удаление",
            "execute" => "Выполнение",
            _ => segments[1]
        };

        return $"{action} {area}";
    }

    private static string GetPermissionCategory(string permissionId)
    {
        var categoryKey = permissionId.Split('.').FirstOrDefault();
        return categoryKey switch
        {
            "organization" => "Организация",
            "users" => "Пользователи",
            "integration" => "Интеграции",
            "access" => "Доступ",
            "audit" => "Аудит",
            "system" => "Система",
            _ => "Прочее"
        };
    }
}

public sealed record PermissionResponse(
    string Id,
    string Name,
    string Description,
    string Category,
    bool IsActive);

public sealed record PartnerPermissionsResponse(Guid ApiKeyId, IReadOnlyCollection<string> PermissionIds);
