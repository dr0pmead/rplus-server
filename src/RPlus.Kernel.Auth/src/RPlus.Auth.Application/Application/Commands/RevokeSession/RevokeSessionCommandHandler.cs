// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Commands.RevokeSession.RevokeSessionCommandHandler
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.SDK.Auth.Commands;
using RPlus.Auth.Domain.Entities;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Commands.RevokeSession;

public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, bool>
{
  private readonly IAuthDataService _authDataService;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<RevokeSessionCommandHandler> _logger;
  private readonly ISecurityMetrics _metrics;

  public RevokeSessionCommandHandler(
    IAuthDataService authDataService,
    TimeProvider timeProvider,
    ILogger<RevokeSessionCommandHandler> logger,
    ISecurityMetrics metrics)
  {
    this._authDataService = authDataService;
    this._timeProvider = timeProvider;
    this._logger = logger;
    this._metrics = metrics;
  }

  public async Task<bool> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
  {
    DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;
    AuthSessionEntity session = (AuthSessionEntity) null;
    if (string.IsNullOrEmpty(request.SessionId))
    {
      this._logger.LogWarning("RevokeSession called without explicit SessionId or Context.");
      return false;
    }
    Guid result;
    if (!Guid.TryParse(request.SessionId, out result))
      return false;
    session = await this._authDataService.GetSessionByIdAsync(result, cancellationToken);
    if (session == null || session.RevokedAt.HasValue)
      return true;
    session.RevokedAt = new DateTime?(now);
    session.RevokeReason = "User requested logout";
    await this._authDataService.UpdateSessionAsync(session, cancellationToken);
    IAuthDataService authDataService = this._authDataService;
    AuditLogEntity log = new AuditLogEntity();
    log.Id = Guid.NewGuid();
    log.UserId = new Guid?(session.UserId);
    log.Action = "session_revoke";
    log.Result = "success";
    log.CreatedAt = now;
    log.Ip = request.ClientIp;
    log.UserAgent = request.UserAgent;
    log.DeviceId = request.DeviceId;
    log.MetadataJson = JsonSerializer.Serialize(new
    {
      sessionId = session.Id
    });
    CancellationToken ct = cancellationToken;
    await authDataService.CreateAuditLogAsync(log, ct);
    this._logger.LogInformation("Session {SessionId} revoked successfully.", (object) session.Id);
    this._metrics.IncSessionRevoked("user_request");
    return true;
  }
}
