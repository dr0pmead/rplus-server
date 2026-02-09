using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Loyalty.Application.Handlers;
using RPlus.SDK.Loyalty.Events;

namespace RPlus.Loyalty.Infrastructure.Consumers;

public class LoyaltyTriggerConsumer : KafkaConsumerBackgroundService<string, LoyaltyTriggerEvent>
{
    private readonly IServiceProvider _serviceProvider;

    public LoyaltyTriggerConsumer(
        IOptions<KafkaOptions> options,
        IServiceProvider serviceProvider,
        ILogger<LoyaltyTriggerConsumer> logger)
        : base(options, logger, LoyaltyEventTopics.Triggered)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleMessageAsync(string key, LoyaltyTriggerEvent message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new ProcessLoyaltyEventCommand(message), cancellationToken);
    }
}
