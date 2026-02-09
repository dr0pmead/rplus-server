// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.AuthDataService
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class AuthDataService : IAuthDataService
{
  private readonly AuthDbContext _db;
  private readonly ILogger<AuthDataService> _logger;

  public AuthDataService(AuthDbContext db, ILogger<AuthDataService> logger)
  {
    this._db = db;
    this._logger = logger;
  }

  public async Task<AuthUserEntity?> GetUserByPhoneHashAsync(string phoneHash, CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthUsers.FirstOrDefaultAsync<AuthUserEntity>((Expression<Func<AuthUserEntity, bool>>) (x => x.PhoneHash == phoneHash), ct);
  }

  public async Task<AuthUserEntity?> GetUserByIdentifierAsync(
    string identifier,
    CancellationToken ct = default (CancellationToken))
  {
    string normalized = identifier.Trim().ToLowerInvariant();
    return await this._db.AuthUsers.FirstOrDefaultAsync<AuthUserEntity>((Expression<Func<AuthUserEntity, bool>>) (x => x.Login == normalized || x.Email == normalized || x.PhoneHash == identifier), ct);
  }

  public async Task<List<AuthUserEntity>> GetUsersByIdentifierAsync(
    string identifier,
    CancellationToken ct = default (CancellationToken))
  {
    string normalized = identifier.Trim().ToLowerInvariant();
    return await this._db.AuthUsers.Where<AuthUserEntity>((Expression<Func<AuthUserEntity, bool>>) (x => x.Login == normalized || x.Email == normalized || x.PhoneHash == identifier)).ToListAsync<AuthUserEntity>(ct);
  }

  public async Task<AuthUserEntity?> GetUserByIdAsync(Guid userId, CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthUsers.FindAsync(new object[1]
    {
      (object) userId
    }, ct);
  }

  public async Task<AuthUserEntity> CreateUserAsync(AuthUserEntity user, CancellationToken ct = default (CancellationToken))
  {
    this._db.AuthUsers.Add(user);
    int num = await this._db.SaveChangesAsync(ct);
    return user;
  }

  public async Task UpdateUserAsync(AuthUserEntity user, CancellationToken ct = default (CancellationToken))
  {
    this._db.AuthUsers.Update(user);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<DeviceEntity?> GetDeviceAsync(
    string deviceKey,
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await this._db.Devices.FirstOrDefaultAsync<DeviceEntity>((Expression<Func<DeviceEntity, bool>>) (d => d.DeviceKey == deviceKey && d.UserId == userId), cancellationToken);
  }

  public async Task CreateDeviceAsync(DeviceEntity device, CancellationToken cancellationToken)
  {
    this._db.Devices.Add(device);
    int num = await this._db.SaveChangesAsync(cancellationToken);
  }

  public async Task<AuthKnownUserEntity?> GetKnownUserByPhoneHashAsync(
    string phoneHash,
    CancellationToken cancellationToken)
  {
    return await this._db.AuthKnownUsers.AsNoTracking<AuthKnownUserEntity>().FirstOrDefaultAsync<AuthKnownUserEntity>((Expression<Func<AuthKnownUserEntity, bool>>) (u => u.PhoneHash == phoneHash), cancellationToken);
  }

  public async Task<AuthCredentialEntity?> GetAuthCredentialAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await this._db.AuthCredentials.AsNoTracking<AuthCredentialEntity>().FirstOrDefaultAsync<AuthCredentialEntity>((Expression<Func<AuthCredentialEntity, bool>>) (c => c.UserId == userId), cancellationToken);
  }

  public async Task AddAuthCredentialAsync(
    AuthCredentialEntity credential,
    CancellationToken cancellationToken)
  {
    this._db.AuthCredentials.Add(credential);
    int num = await this._db.SaveChangesAsync(cancellationToken);
  }

  public async Task AddAuthRecoveryAsync(AuthRecoveryEntity recovery, CancellationToken ct)
  {
    this._db.AuthRecoveries.Add(recovery);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<List<PasskeyCredentialEntity>> GetPasskeyCredentialsAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await this._db.PasskeyCredentials.AsNoTracking<PasskeyCredentialEntity>().Where<PasskeyCredentialEntity>((Expression<Func<PasskeyCredentialEntity, bool>>) (c => c.UserId == userId)).ToListAsync<PasskeyCredentialEntity>(cancellationToken);
  }

  public async Task AddPasskeyCredentialAsync(
    PasskeyCredentialEntity credential,
    CancellationToken cancellationToken)
  {
    this._db.PasskeyCredentials.Add(credential);
    int num = await this._db.SaveChangesAsync(cancellationToken);
  }

  public async Task<PasskeyCredentialEntity?> GetPasskeyCredentialByDescriptorIdAsync(
    byte[] descriptorId,
    CancellationToken cancellationToken)
  {
    return await this._db.PasskeyCredentials.FirstOrDefaultAsync<PasskeyCredentialEntity>((Expression<Func<PasskeyCredentialEntity, bool>>) (c => c.DescriptorId == descriptorId), cancellationToken);
  }

  public async Task<AuthKnownUserEntity?> GetKnownUserByUserIdAsync(
    Guid userId,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthKnownUsers.FindAsync(new object[1]
    {
      (object) userId
    }, ct);
  }

  public async Task<AuthKnownUserEntity> CreateKnownUserAsync(AuthKnownUserEntity knownUser, CancellationToken ct = default(CancellationToken))
  {
    this._db.AuthKnownUsers.Add(knownUser);
    int num = await this._db.SaveChangesAsync(ct);
    return knownUser;
  }

  public async Task<AuthSessionEntity?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthSessions.Include<AuthSessionEntity, AuthUserEntity>((Expression<Func<AuthSessionEntity, AuthUserEntity>>) (x => x.User)).FirstOrDefaultAsync<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (x => x.Id == sessionId), ct);
  }

  public async Task<AuthSessionEntity?> GetAuthSessionByUserIdAsync(
    Guid userId,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthSessions.Include<AuthSessionEntity, AuthUserEntity>((Expression<Func<AuthSessionEntity, AuthUserEntity>>) (x => x.User)).Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (x => x.UserId == userId && x.RevokedAt == new DateTime?() && x.ExpiresAt > DateTime.UtcNow)).OrderByDescending<AuthSessionEntity, DateTime?>((Expression<Func<AuthSessionEntity, DateTime?>>) (x => x.LastActivityAt)).FirstOrDefaultAsync<AuthSessionEntity>(ct);
  }

  public async Task<AuthSessionEntity?> GetAuthSessionByPhoneHashAsync(
    string phoneHash,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthSessions.Include<AuthSessionEntity, AuthUserEntity>((Expression<Func<AuthSessionEntity, AuthUserEntity>>) (x => x.User)).Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (x => x.User.PhoneHash == phoneHash && x.RevokedAt == new DateTime?() && x.ExpiresAt > DateTime.UtcNow)).OrderByDescending<AuthSessionEntity, DateTime?>((Expression<Func<AuthSessionEntity, DateTime?>>) (x => x.LastActivityAt)).FirstOrDefaultAsync<AuthSessionEntity>(ct);
  }

  public async Task<List<AuthSessionEntity>> GetActiveSessionsByUserIdAsync(
    Guid userId,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AuthSessions.Where<AuthSessionEntity>((Expression<Func<AuthSessionEntity, bool>>) (x => x.UserId == userId && x.RevokedAt == new DateTime?() && x.ExpiresAt > DateTime.UtcNow)).ToListAsync<AuthSessionEntity>(ct);
  }

  public async Task<AuthSessionEntity> CreateSessionAsync(
    AuthSessionEntity session,
    CancellationToken ct = default (CancellationToken))
  {
    this._db.AuthSessions.Add(session);
    int num = await this._db.SaveChangesAsync(ct);
    return session;
  }

  public async Task UpdateSessionAsync(AuthSessionEntity session, CancellationToken ct = default (CancellationToken))
  {
    this._db.AuthSessions.Update(session);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct = default (CancellationToken))
  {
    AuthSessionEntity async = await this._db.AuthSessions.FindAsync(new object[1]
    {
      (object) sessionId
    }, ct);
    if (async == null)
      return;
    async.RevokedAt = new DateTime?(DateTime.UtcNow);
    async.RevokeReason = reason;
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<DeviceEntity?> GetDeviceByUserAndKeyAsync(
    Guid userId,
    string deviceKey,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.Devices.FirstOrDefaultAsync<DeviceEntity>((Expression<Func<DeviceEntity, bool>>) (x => x.UserId == userId && x.DeviceKey == deviceKey), ct);
  }

  public async Task<DeviceEntity> CreateOrUpdateDeviceAsync(
    DeviceEntity device,
    CancellationToken ct = default (CancellationToken))
  {
    DeviceEntity existing = await this._db.Devices.FirstOrDefaultAsync<DeviceEntity>((Expression<Func<DeviceEntity, bool>>) (x => x.UserId == device.UserId && x.DeviceKey == device.DeviceKey), ct);
    if (existing == null)
    {
      this._db.Devices.Add(device);
      int num = await this._db.SaveChangesAsync(ct);
      return device;
    }
    existing.PublicJwk = device.PublicJwk;
    existing.LastSeenAt = DateTime.UtcNow;
    existing.IsBlocked = device.IsBlocked;
    int num1 = await this._db.SaveChangesAsync(ct);
    return existing;
  }

  public async Task<OtpChallengeEntity?> GetLatestOtpChallengeAsync(
    string phoneHash,
    string deviceId,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.OtpChallenges.Where<OtpChallengeEntity>((Expression<Func<OtpChallengeEntity, bool>>) (x => x.PhoneHash == phoneHash && x.IssuerDeviceId == deviceId && x.VerifiedAt == new DateTime?())).OrderByDescending<OtpChallengeEntity, DateTime>((Expression<Func<OtpChallengeEntity, DateTime>>) (x => x.CreatedAt)).FirstOrDefaultAsync<OtpChallengeEntity>(ct);
  }

  public async Task<OtpChallengeEntity> CreateOtpChallengeAsync(
    OtpChallengeEntity challenge,
    CancellationToken ct = default (CancellationToken))
  {
    this._db.OtpChallenges.Add(challenge);
    int num = await this._db.SaveChangesAsync(ct);
    return challenge;
  }

  public async Task UpdateOtpChallengeAsync(OtpChallengeEntity challenge, CancellationToken ct = default (CancellationToken))
  {
    this._db.OtpChallenges.Update(challenge);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<int> CountOtpChallengesAsync(
    string phoneHash,
    string deviceId,
    DateTime since,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.OtpChallenges.CountAsync<OtpChallengeEntity>((Expression<Func<OtpChallengeEntity, bool>>) (x => x.PhoneHash == phoneHash && x.IssuerDeviceId == deviceId && x.CreatedAt >= since), ct);
  }

  public async Task<RefreshTokenEntity?> GetRefreshTokenByHashAsync(
    string tokenHash,
    CancellationToken ct = default (CancellationToken))
  {
    return await this._db.RefreshTokens.Include<RefreshTokenEntity, DeviceEntity>((Expression<Func<RefreshTokenEntity, DeviceEntity>>) (x => x.Device)).FirstOrDefaultAsync<RefreshTokenEntity>((Expression<Func<RefreshTokenEntity, bool>>) (x => x.TokenHash == tokenHash), ct);
  }

  public async Task<RefreshTokenEntity> CreateRefreshTokenAsync(
    RefreshTokenEntity token,
    CancellationToken ct = default (CancellationToken))
  {
    this._db.RefreshTokens.Add(token);
    int num = await this._db.SaveChangesAsync(ct);
    return token;
  }

  public async Task UpdateRefreshTokenAsync(RefreshTokenEntity token, CancellationToken ct = default (CancellationToken))
  {
    this._db.RefreshTokens.Update(token);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task RevokeRefreshTokenFamilyAsync(
    string tokenFamily,
    string reason,
    CancellationToken ct = default (CancellationToken))
  {
    DbSet<RefreshTokenEntity> refreshTokens = this._db.RefreshTokens;
    Expression<Func<RefreshTokenEntity, bool>> predicate = (Expression<Func<RefreshTokenEntity, bool>>) (x => x.TokenFamily == tokenFamily && x.RevokedAt == new DateTime?());
    foreach (RefreshTokenEntity refreshTokenEntity in await refreshTokens.Where<RefreshTokenEntity>(predicate).ToListAsync<RefreshTokenEntity>(ct))
      refreshTokenEntity.RevokedAt = new DateTime?(DateTime.UtcNow);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<AbuseCounterEntity?> GetAbuseCounterAsync(string key, CancellationToken ct = default (CancellationToken))
  {
    return await this._db.AbuseCounters.FindAsync(new object[1]
    {
      (object) key
    }, ct);
  }

  public async Task<AbuseCounterEntity> CreateOrUpdateAbuseCounterAsync(
    AbuseCounterEntity counter,
    CancellationToken ct = default (CancellationToken))
  {
    AbuseCounterEntity existing = await this._db.AbuseCounters.FindAsync(new object[1]
    {
      (object) counter.Key
    }, ct);
    if (existing == null)
    {
      this._db.AbuseCounters.Add(counter);
    }
    else
    {
      existing.Counter = counter.Counter;
      existing.WindowExpiresAt = counter.WindowExpiresAt;
    }
    int num = await this._db.SaveChangesAsync(ct);
    AbuseCounterEntity abuseCounterAsync = existing ?? counter;
    existing = (AbuseCounterEntity) null;
    return abuseCounterAsync;
  }

  public async Task CreateAuditLogAsync(AuditLogEntity log, CancellationToken ct = default (CancellationToken))
  {
    this._db.AuditLogs.Add(log);
    int num = await this._db.SaveChangesAsync(ct);
  }

  public async Task<AuthKnownUserEntity?> GetKnownUserByIdAsync(Guid userId, CancellationToken ct = default(CancellationToken))
  {
    return await this._db.AuthKnownUsers.FirstOrDefaultAsync(x => x.UserId == userId, ct);
  }

  public async Task UpdateKnownUserAsync(AuthKnownUserEntity knownUser, CancellationToken ct = default(CancellationToken))
  {
    this._db.AuthKnownUsers.Update(knownUser);
    await this._db.SaveChangesAsync(ct);
  }

  public async Task RevokeAllUserSessionsAsync(Guid userId, string reason, CancellationToken ct = default(CancellationToken))
  {
    var activeSessions = await this._db.AuthSessions
      .Where(x => x.UserId == userId && x.RevokedAt == null)
      .ToListAsync(ct);

    foreach (var session in activeSessions)
    {
      session.RevokedAt = DateTime.UtcNow;
      session.RevokeReason = reason;
    }

    await this._db.SaveChangesAsync(ct);
  }
}
