// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.IAuthDataService
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using RPlus.Auth.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface IAuthDataService
{
  Task<AuthUserEntity?> GetUserByIdAsync(Guid userId, CancellationToken ct = default (CancellationToken));

  Task<AuthUserEntity?> GetUserByPhoneHashAsync(string phoneHash, CancellationToken ct = default (CancellationToken));

  Task<AuthUserEntity?> GetUserByIdentifierAsync(string identifier, CancellationToken ct = default (CancellationToken));

  Task<List<AuthUserEntity>> GetUsersByIdentifierAsync(string identifier, CancellationToken ct = default (CancellationToken));

  Task<AuthUserEntity> CreateUserAsync(AuthUserEntity user, CancellationToken ct = default (CancellationToken));

  Task UpdateUserAsync(AuthUserEntity user, CancellationToken ct = default (CancellationToken));

  Task<DeviceEntity?> GetDeviceAsync(
    string deviceKey,
    Guid userId,
    CancellationToken cancellationToken);

  Task CreateDeviceAsync(DeviceEntity device, CancellationToken cancellationToken);

  Task<DeviceEntity?> GetDeviceByUserAndKeyAsync(
    Guid userId,
    string deviceKey,
    CancellationToken ct = default (CancellationToken));

  Task<DeviceEntity> CreateOrUpdateDeviceAsync(DeviceEntity device, CancellationToken ct = default (CancellationToken));

  Task<AuthCredentialEntity?> GetAuthCredentialAsync(
    Guid userId,
    CancellationToken cancellationToken);

  Task AddAuthCredentialAsync(AuthCredentialEntity credential, CancellationToken cancellationToken);

  Task AddAuthRecoveryAsync(AuthRecoveryEntity recovery, CancellationToken ct);

  Task<List<PasskeyCredentialEntity>> GetPasskeyCredentialsAsync(
    Guid userId,
    CancellationToken cancellationToken);

  Task AddPasskeyCredentialAsync(
    PasskeyCredentialEntity credential,
    CancellationToken cancellationToken);

  Task<PasskeyCredentialEntity?> GetPasskeyCredentialByDescriptorIdAsync(
    byte[] descriptorId,
    CancellationToken cancellationToken);

  Task<AuthKnownUserEntity?> GetKnownUserByPhoneHashAsync(string phoneHash, CancellationToken ct = default (CancellationToken));

  Task<AuthKnownUserEntity?> GetKnownUserByUserIdAsync(Guid userId, CancellationToken ct = default (CancellationToken));
  
  Task<AuthKnownUserEntity> CreateKnownUserAsync(AuthKnownUserEntity knownUser, CancellationToken ct = default(CancellationToken));

  Task<AuthKnownUserEntity?> GetKnownUserByIdAsync(Guid userId, CancellationToken ct = default(CancellationToken));

  Task UpdateKnownUserAsync(AuthKnownUserEntity knownUser, CancellationToken ct = default(CancellationToken));

  Task RevokeAllUserSessionsAsync(Guid userId, string reason, CancellationToken ct = default(CancellationToken));

  Task<AuthSessionEntity?> GetSessionByIdAsync(Guid sessionId, CancellationToken ct = default (CancellationToken));

  Task<AuthSessionEntity?> GetAuthSessionByUserIdAsync(Guid userId, CancellationToken ct = default (CancellationToken));

  Task<AuthSessionEntity?> GetAuthSessionByPhoneHashAsync(string phoneHash, CancellationToken ct = default (CancellationToken));

  Task<List<AuthSessionEntity>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken ct = default (CancellationToken));

  Task<AuthSessionEntity> CreateSessionAsync(AuthSessionEntity session, CancellationToken ct = default (CancellationToken));

  Task UpdateSessionAsync(AuthSessionEntity session, CancellationToken ct = default (CancellationToken));

  Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct = default (CancellationToken));

  Task<OtpChallengeEntity?> GetLatestOtpChallengeAsync(
    string phoneHash,
    string deviceId,
    CancellationToken ct = default (CancellationToken));

  Task<OtpChallengeEntity> CreateOtpChallengeAsync(
    OtpChallengeEntity challenge,
    CancellationToken ct = default (CancellationToken));

  Task UpdateOtpChallengeAsync(OtpChallengeEntity challenge, CancellationToken ct = default (CancellationToken));

  Task<int> CountOtpChallengesAsync(
    string phoneHash,
    string deviceId,
    DateTime since,
    CancellationToken ct = default (CancellationToken));

  Task<RefreshTokenEntity?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken ct = default (CancellationToken));

  Task<RefreshTokenEntity> CreateRefreshTokenAsync(RefreshTokenEntity token, CancellationToken ct = default (CancellationToken));

  Task UpdateRefreshTokenAsync(RefreshTokenEntity token, CancellationToken ct = default (CancellationToken));

  Task RevokeRefreshTokenFamilyAsync(string tokenFamily, string reason, CancellationToken ct = default (CancellationToken));

  Task CreateAuditLogAsync(AuditLogEntity log, CancellationToken ct = default (CancellationToken));

  Task<AbuseCounterEntity?> GetAbuseCounterAsync(string key, CancellationToken ct = default (CancellationToken));

  Task<AbuseCounterEntity> CreateOrUpdateAbuseCounterAsync(
    AbuseCounterEntity counter,
    CancellationToken ct = default (CancellationToken));
}
