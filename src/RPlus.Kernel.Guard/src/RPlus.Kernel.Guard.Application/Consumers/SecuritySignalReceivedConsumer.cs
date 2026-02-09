using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using RPlus.Kernel.Guard.Domain.Services;
using RPlus.SDK.Contracts.Domain.Security;
using RPlus.SDK.Security.Enums;
using RPlus.SDK.Security.Helpers;

namespace RPlus.Kernel.Guard.Application.Consumers;

public class SecuritySignalReceivedConsumer : IConsumer<SecuritySignalReceived_v1>
{
    private readonly IGuardStateStore _stateStore;
    private readonly IDistributedCache _cache; // For dedup
    private readonly ILogger<SecuritySignalReceivedConsumer> _logger;

    public SecuritySignalReceivedConsumer(
        IGuardStateStore stateStore,
        IDistributedCache cache,
        ILogger<SecuritySignalReceivedConsumer> logger)
    {
        _stateStore = stateStore;
        _cache = cache;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SecuritySignalReceived_v1> context)
    {
        var msg = context.Message;
        var subject = msg.Subject.IpAddress;

        // 1. Idempotency Check (TimeBucket e.g. 1 minute)
        long timeBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60; 
        var dedupKey = GuardRedisKeys.SignalDedup(subject, msg.SignalType, timeBucket);

        var existing = await _cache.GetStringAsync(dedupKey);
        if (existing != null)
        {
            _logger.LogDebug("Signal {SignalType} for {Subject} deduplicated (Bucket: {Bucket})", msg.SignalType, subject, timeBucket);
            return;
        }

        // Mark processed (TTL 90s to cover boundary)
        await _cache.SetStringAsync(dedupKey, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90) });

        // 2. Process Signal / Escalate
        if (msg.Score > 0.8)
        {
            var currentThreat = await _stateStore.GetThreatLevelAsync(subject);
            if (currentThreat < ThreatLevel.High)
            {
                _logger.LogWarning("Escalating threat for {Subject} to High due to signal {SignalType} (Score: {Score})", subject, msg.SignalType, msg.Score);
                await _stateStore.SetThreatLevelAsync(subject, ThreatLevel.High, TimeSpan.FromMinutes(10));
            }
        }
        else if (msg.Score > 0.5)
        {
             var currentThreat = await _stateStore.GetThreatLevelAsync(subject);
             if (currentThreat < ThreatLevel.Medium)
             {
                 await _stateStore.SetThreatLevelAsync(subject, ThreatLevel.Medium, TimeSpan.FromMinutes(5));
             }
        }
    }
}
