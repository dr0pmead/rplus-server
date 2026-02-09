// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.IntegrationKeysController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPlus.Kernel.Integration.Application;
using RPlus.Kernel.Integration.Domain.Entities;
using RPlus.Kernel.Integration.Application.Events;
using RPlus.Kernel.Integration.Infrastructure.Services;
using RPlus.SDK.Eventing.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("api/integration/keys")]
public class IntegrationKeysController : ControllerBase
{
  private readonly IntegrationAdminService _adminService;
  private readonly IIntegrationDbContext _db;
  private readonly IEventPublisher _events;

  public IntegrationKeysController(IntegrationAdminService adminService, IIntegrationDbContext db, IEventPublisher events)
  {
    this._adminService = adminService;
    this._db = db;
    this._events = events;
  }

  [HttpGet]
  public async Task<IActionResult> GetKeys([FromQuery] Guid? partnerId)
  {
    IntegrationKeysController integrationKeysController = this;
    IQueryable<IntegrationApiKey> source = integrationKeysController._db.ApiKeys.AsNoTracking<IntegrationApiKey>();
    if (partnerId.HasValue)
      source = source.Where<IntegrationApiKey>((Expression<Func<IntegrationApiKey, bool>>) (k => k.PartnerId == partnerId));
    List<IntegrationApiKey> listAsync = await source.ToListAsync<IntegrationApiKey>();
    return (IActionResult) integrationKeysController.Ok((object) new
    {
      items = listAsync,
      totalCount = listAsync.Count
    });
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetKey(Guid id)
  {
    IntegrationKeysController integrationKeysController = this;
    IntegrationApiKey async = await integrationKeysController._db.ApiKeys.FindAsync((object) id);
    return async != null ? (IActionResult) integrationKeysController.Ok((object) async) : (IActionResult) integrationKeysController.NotFound();
  }

  [HttpPost]
  public async Task<IActionResult> GenerateKey([FromBody] GenerateKeyRequest request)
  {
    if (!request.PartnerId.HasValue)
      return BadRequest(new { error = "PartnerId is required" });

    var cancellationToken = HttpContext.RequestAborted;
    try
    {
      var (apiKey, apiKeyRaw, hmacSecret) = await _adminService.CreateApiKeyAsync(
          request.PartnerId.Value,
          request.Environment,
          Array.Empty<string>(),
          new Dictionary<string, int>(),
          request.ExpiresAt,
          request.RequireSignature ?? false,
          cancellationToken);

      var fullKey = $"{apiKey.Prefix}{apiKeyRaw}";

      var createdEvent = new IntegrationKeyCreatedEvent(
          apiKey.Id,
          apiKey.PartnerId ?? Guid.Empty,
          apiKey.Environment,
          apiKey.Prefix,
          apiKey.CreatedAt);
      await _events.PublishAsync(createdEvent, IntegrationKeyCreatedEvent.EventName, apiKey.Id.ToString(), cancellationToken);

      return Ok(new
      {
        apiKey.Id,
        Key = fullKey,
        HmacSecret = hmacSecret,
        apiKey.CreatedAt,
        apiKey.ExpiresAt,
        apiKey.Status
      });
    }
    catch (InvalidOperationException ex) when (ex.Message == "key_already_exists")
    {
      return Conflict(new { error = "key_already_exists" });
    }
  }

  [HttpPatch("{id}")]
  public async Task<IActionResult> UpdateKey(Guid id, [FromBody] UpdateKeyRequest request)
  {
    IntegrationKeysController integrationKeysController = this;
    IntegrationApiKey key = await integrationKeysController._db.ApiKeys.FindAsync((object) id);
    if (key == null)
      return (IActionResult) integrationKeysController.NotFound();
    key.Update(request.Status ?? key.Status, request.RequireSignature ?? key.RequireSignature, request.ExpiresAt ?? key.ExpiresAt);
    int num = await integrationKeysController._db.SaveChangesAsync(CancellationToken.None);
    await _adminService.InvalidateKeyCacheAsync(key, HttpContext.RequestAborted);
    return (IActionResult) integrationKeysController.Ok((object) key);
  }

  [HttpPost("{id}/revoke")]
  public async Task<IActionResult> RevokeKey(Guid id)
  {
    var cancellationToken = HttpContext.RequestAborted;
    IntegrationApiKey key = await _db.ApiKeys.FindAsync((object) id);
    if (key == null)
      return NotFound();

    await _adminService.RevokeKeyAsync(id, cancellationToken);

    var revokedEvent = new IntegrationKeyRevokedEvent(
        key.Id,
        key.PartnerId ?? Guid.Empty,
        key.Environment,
        DateTime.UtcNow);
    await _events.PublishAsync(revokedEvent, IntegrationKeyRevokedEvent.EventName, key.Id.ToString(), cancellationToken);

    return Ok(key);
  }

  [HttpPost("{id}/rotate")]
  public async Task<IActionResult> RotateKey(Guid id)
  {
    var cancellationToken = HttpContext.RequestAborted;
    var (apiKey, newApiKeyRaw, newHmacSecret) = await _adminService.RotateKeyAsync(id, cancellationToken);
    var fullKey = $"{apiKey.Prefix}{newApiKeyRaw}";

    var rotatedEvent = new IntegrationKeyRotatedEvent(
        apiKey.Id,
        apiKey.PartnerId ?? Guid.Empty,
        apiKey.Environment,
        DateTime.UtcNow);
    await _events.PublishAsync(rotatedEvent, IntegrationKeyRotatedEvent.EventName, apiKey.Id.ToString(), cancellationToken);

    return Ok(new
    {
      apiKey.Id,
      Key = fullKey,
      HmacSecret = newHmacSecret,
      apiKey.CreatedAt,
      apiKey.ExpiresAt,
      apiKey.Status
    });
  }

  private static string ComputeHash(string input)
  {
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToBase64String(shA256.ComputeHash(Encoding.UTF8.GetBytes(input)));
  }
}
