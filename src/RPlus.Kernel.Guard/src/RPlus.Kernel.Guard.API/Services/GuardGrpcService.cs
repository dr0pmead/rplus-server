using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using RPlus.Kernel.Guard.Application.Pipelines;
using RPlus.SDK.Security.Models;
using RPlusGrpc.Guard;

namespace RPlus.Kernel.Guard.Api.Services;

public class GuardGrpcService : GuardService.GuardServiceBase
{
    private readonly ISecurityPipeline _pipeline;
    private readonly RPlus.Kernel.Guard.Infrastructure.Persistence.GuardDbContext _dbContext;

    public GuardGrpcService(ISecurityPipeline pipeline, RPlus.Kernel.Guard.Infrastructure.Persistence.GuardDbContext dbContext)
    {
        _pipeline = pipeline;
        _dbContext = dbContext;
    }

    public override async Task<CheckResponse> Check(CheckRequest request, ServerCallContext context)
    {
        var subject = new SecuritySubject(request.IpAddress);
        var requestId = Guid.NewGuid().ToString();
        var securityContext = new SecurityContext(
            subject,
            request.Route,
            new Dictionary<string, string>(request.Headers),
            requestId
        );

        var decision = await _pipeline.EvaluateAsync(securityContext);

        var evt = new RPlus.SDK.Contracts.Domain.Security.SecurityDecisionMade_v1(
            subject,
            decision,
            request.Route,
            requestId
        )
        {
            MessageId = Guid.NewGuid(),
            SourceService = "rplus-guard",
            Timestamp = DateTime.UtcNow
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(evt);
        _dbContext.OutboxMessages.Add(new RPlus.SDK.Infrastructure.Outbox.OutboxMessage
        {
            Id = evt.MessageId,
            EventName = "SecurityDecisionMade_v1",
            Payload = json,
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[Error] Failed to save audit log: {ex.Message}");
        }

        return new CheckResponse
        {
            IsAllowed = decision.Type == RPlus.SDK.Security.Enums.DecisionType.Allow,
            DecisionType = (int)decision.Type,
            ThreatLevel = (int)decision.ThreatLevel,
            Reason = decision.ReasonCode,
            TtlSeconds = decision.TtlSeconds ?? 0,
            IsTerminal = decision.IsTerminal,
            PolicyId = decision.PolicyId ?? "",
            EffectiveThreatLevel = (int)decision.EffectiveThreatLevel
        };
    }

    public override Task<CreateChallengeResponse> CreateChallenge(CreateChallengeRequest request, ServerCallContext context)
    {
        // Basic stub implementation
        return Task.FromResult(new CreateChallengeResponse
        {
            ChallengeId = Guid.NewGuid().ToString(),
            Salt = Guid.NewGuid().ToString("N"),
            Difficulty = 4,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            Scope = request.Scope
        });
    }

    public override Task<VerifyPowResponse> VerifyPow(VerifyPowRequest request, ServerCallContext context)
    {
        // Basic stub implementation - always valid for now to facilitate dev
        return Task.FromResult(new VerifyPowResponse
        {
            IsValid = true,
            Hash = "dummy-hash"
        });
    }
}
