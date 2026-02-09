// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Api.Controllers.GuardPoliciesController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6C1F5346-815B-4C0D-BD63-391C84B5BE3F
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using RPlus.Kernel.Guard.Infrastructure.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Guard.Api.Controllers;

[ApiController]
[Route("api/guard/policies")]
public class GuardPoliciesController : ControllerBase
{
  private readonly GuardPolicyStore _store;
  private readonly IConfiguration _configuration;

  public GuardPoliciesController(GuardPolicyStore store, IConfiguration configuration)
  {
    this._store = store;
    this._configuration = configuration;
  }

  [HttpGet("maintenance")]
  public async Task<IActionResult> GetMaintenance(CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    GuardMaintenanceStatus maintenanceAsync = await policiesController._store.GetMaintenanceAsync(cancellationToken);
    return (IActionResult) policiesController.Ok((object) maintenanceAsync);
  }

  [HttpPost("maintenance")]
  public async Task<IActionResult> SetMaintenance(
    [FromBody] MaintenanceRequest request,
    CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    TimeSpan? ttl = request.TtlSeconds.HasValue ? new TimeSpan?(TimeSpan.FromSeconds((long) request.TtlSeconds.Value)) : new TimeSpan?();
    await policiesController._store.SetMaintenanceAsync(request.Enabled, request.Reason, ttl, cancellationToken);
    return (IActionResult) policiesController.NoContent();
  }

  [HttpPost("ip/block")]
  public async Task<IActionResult> BlockIp([FromBody] IpRequest request, CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.Ip))
      return (IActionResult) policiesController.BadRequest((object) new
      {
        error = "ip_required"
      });
    TimeSpan? ttl = request.TtlSeconds.HasValue ? new TimeSpan?(TimeSpan.FromSeconds((long) request.TtlSeconds.Value)) : new TimeSpan?();
    await policiesController._store.BlockIpAsync(request.Ip, request.Reason, ttl, cancellationToken);
    return (IActionResult) policiesController.NoContent();
  }

  [HttpPost("ip/unblock")]
  public async Task<IActionResult> UnblockIp([FromBody] IpRequest request, CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.Ip))
      return (IActionResult) policiesController.BadRequest((object) new
      {
        error = "ip_required"
      });
    await policiesController._store.UnblockIpAsync(request.Ip, cancellationToken);
    return (IActionResult) policiesController.NoContent();
  }

  [HttpGet("ip/status")]
  public async Task<IActionResult> GetIpStatus([FromQuery] string ip, CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    if (string.IsNullOrWhiteSpace(ip))
      return (IActionResult) policiesController.BadRequest((object) new
      {
        error = "ip_required"
      });
    GuardIpBlockStatus ipStatusAsync = await policiesController._store.GetIpStatusAsync(ip, cancellationToken);
    return (IActionResult) policiesController.Ok((object) ipStatusAsync);
  }

  [HttpGet("rps")]
  public async Task<IActionResult> GetRps(CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    GuardRpsStatus rpsAsync = await policiesController._store.GetRpsAsync(cancellationToken);
    return (IActionResult) policiesController.Ok((object) rpsAsync);
  }

  [HttpPost("rps")]
  public async Task<IActionResult> SetRps([FromBody] RpsRequest request, CancellationToken cancellationToken)
  {
    GuardPoliciesController policiesController = this;
    if (!policiesController.IsAdminAuthorized())
      return (IActionResult) policiesController.Unauthorized();
    if (request.Enabled && (request.Limit <= 0 || request.WindowSeconds <= 0))
      return (IActionResult) policiesController.BadRequest((object) new
      {
        error = "invalid_rps_settings"
      });
    TimeSpan? ttl = request.TtlSeconds.HasValue ? new TimeSpan?(TimeSpan.FromSeconds((long) request.TtlSeconds.Value)) : new TimeSpan?();
    await policiesController._store.SetRpsAsync(request.Enabled, request.Limit, request.WindowSeconds, ttl, cancellationToken);
    return (IActionResult) policiesController.NoContent();
  }

  private bool IsAdminAuthorized()
  {
    string b = this._configuration["Guard:AdminKey"];
    if (string.IsNullOrWhiteSpace(b))
      return true;
    StringValues stringValues;
    return this.Request.Headers.TryGetValue("X-Guard-Admin-Key", out stringValues) && string.Equals(stringValues.ToString(), b, StringComparison.Ordinal);
  }
}
