using System;
using System.Threading.Tasks;
using RPlus.SDK.Security.Enums;
using RPlus.SDK.Security.Models;

namespace RPlus.Kernel.Guard.Domain.Services;

public interface IGuardStateStore
{
    // Rate Limiting
    Task<(long count, long ttl)> IncrementRateLimitAsync(string key, int windowSeconds);
    
    // Blocking / Threat State
    Task SetThreatLevelAsync(string ip, ThreatLevel level, TimeSpan ttl);
    Task<ThreatLevel> GetThreatLevelAsync(string ip);
    
    Task BlockSubjectAsync(string key, TimeSpan duration, string reason);
    Task<bool> IsBlockedAsync(string key);
    
    // Challenges
    Task SetChallengeAsync(SecurityChallenge challenge);
    Task<SecurityChallenge?> GetChallengeAsync(string challengeId);
    Task RemoveChallengeAsync(string challengeId);
}
