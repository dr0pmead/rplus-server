using Grpc.Core;
using Microsoft.Extensions.Logging;
using RPlus.Auth.Application.Interfaces;
using RPlusGrpc.Guard;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class GuardPowProtectionService : IProtectionService
{
    private readonly GuardService.GuardServiceClient _guardClient;
    private readonly ILogger<GuardPowProtectionService> _logger;

    public GuardPowProtectionService(GuardService.GuardServiceClient guardClient, ILogger<GuardPowProtectionService> logger)
    {
        _guardClient = guardClient;
        _logger = logger;
    }

    public async Task<bool> VerifySolutionAsync(string challengeId, string nonce, string? ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(nonce))
            return false;

        try
        {
            var response = await _guardClient.VerifyPowAsync(new VerifyPowRequest
            {
                ChallengeId = challengeId,
                Nonce = nonce,
                IpAddress = ipAddress ?? string.Empty
            }, cancellationToken: ct);

            return response.IsValid;
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning(ex, "Guard PoW verification unavailable");
            return false;
        }
        catch (RpcException ex)
        {
            _logger.LogInformation(ex, "Guard PoW verification rejected");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guard PoW verification failed unexpectedly");
            return false;
        }
    }
}

