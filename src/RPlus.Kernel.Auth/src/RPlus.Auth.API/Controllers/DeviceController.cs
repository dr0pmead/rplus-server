// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Api.Controllers.DeviceController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: E1DD3203-690E-448F-89A2-ED7CA219063C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Core.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Api.Controllers;

[ApiController]
[Route("devices")]
[Route("api/auth/devices")]
public sealed class DeviceController : ControllerBase
{
  private readonly IAuthDataService _authDataService;
  private readonly SystemApiOptions _systemApi;
  private readonly ILogger<DeviceController> _logger;

  public DeviceController(
    IAuthDataService authDataService,
    IOptions<SystemApiOptions> systemApi,
    ILogger<DeviceController> logger)
  {
    this._authDataService = authDataService;
    this._systemApi = systemApi.Value;
    this._logger = logger;
  }

  [HttpGet("validate")]
  [ProducesResponseType(typeof (DeviceController.DeviceValidationResponse), 200)]
  [ProducesResponseType(401)]
  public async Task<IActionResult> ValidateDevice(
    [FromQuery] Guid userId,
    [FromQuery] string deviceId,
    CancellationToken cancellationToken)
  {
    DeviceController deviceController = this;
    StringValues stringValues;
    if (!deviceController.Request.Headers.TryGetValue("X-System-Api-Key", out stringValues) || stringValues != deviceController._systemApi.ApiKey)
      return (IActionResult) deviceController.Unauthorized((object) new
      {
        error = "invalid_system_api_key"
      });
    if (userId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId))
      return (IActionResult) deviceController.BadRequest((object) new
      {
        error = "userId_and_deviceId_required"
      });
    try
    {
      DeviceEntity byUserAndKeyAsync = await deviceController._authDataService.GetDeviceByUserAndKeyAsync(userId, deviceId, cancellationToken);
      bool Valid = byUserAndKeyAsync != null && !byUserAndKeyAsync.IsBlocked;
      deviceController._logger.LogInformation("Device validation. UserId={UserId}, DeviceId={DeviceId}, Valid={Valid}", (object) userId, (object) deviceId, (object) Valid);
      return (IActionResult) deviceController.Ok((object) new DeviceController.DeviceValidationResponse(Valid, deviceId, userId));
    }
    catch (Exception ex)
    {
      deviceController._logger.LogError(ex, "Error validating device. UserId={UserId}, DeviceId={DeviceId}", (object) userId, (object) deviceId);
      return (IActionResult) deviceController.StatusCode(500, (object) new
      {
        error = "validation_failed"
      });
    }
  }

  public sealed record DeviceValidationResponse(bool Valid, string DeviceId, Guid UserId);
}
