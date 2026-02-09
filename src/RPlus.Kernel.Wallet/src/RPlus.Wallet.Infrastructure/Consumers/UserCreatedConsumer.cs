using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Core.Messaging.Events;
using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;

#nullable enable
namespace RPlus.Wallet.Infrastructure.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreated>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<UserCreatedConsumer> _logger;

    public UserCreatedConsumer(
        IWalletRepository walletRepository,
        IEncryptionService encryptionService,
        ILogger<UserCreatedConsumer> logger)
    {
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreated> context)
    {
        var userId = context.Message.UserId;
        if (string.Equals(context.Message.UserType, "System", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping wallet provisioning for system user {UserId}", userId);
            return;
        }
        _logger.LogInformation("Ensuring wallet exists for user {UserId}", userId);

        try
        {
            if (await _walletRepository.GetByUserIdForUpdateAsync(userId, context.CancellationToken) != null)
            {
                _logger.LogDebug("Wallet already provisioned for {UserId}", userId);
                return;
            }

            var wallet = new Domain.Entities.Wallet(userId, _encryptionService.Encrypt(0L), _encryptionService.GetCurrentKeyId());
            await _walletRepository.AddAsync(wallet, context.CancellationToken);
            await _walletRepository.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Wallet provisioned for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision wallet for {UserId}", userId);
            throw;
        }
    }
}
