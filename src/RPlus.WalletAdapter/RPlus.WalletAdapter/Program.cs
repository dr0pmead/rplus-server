using MassTransit;
using RPlus.WalletAdapter.Consumers;
using RPlus.WalletAdapter.Models;
using RPlusGrpc.Wallet;
using RPlus.SDK.Eventing;
using RPlus.SDK.Contracts.Events;
using LoyaltyPointsAccrued = RPlus.SDK.Contracts.Domain.Loyalty.LoyaltyPointsAccrued_v1;
using LoyaltyPointsAccrualRequested = RPlus.SDK.Contracts.Domain.Loyalty.LoyaltyPointsAccrualRequested_v1;

var builder = Host.CreateApplicationBuilder(args);

// Vault — load secrets (must be before any service reads config)
builder.Configuration.AddVault("walletadapter");

// Configuration
var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"] ?? "kernel-kafka:9092";
var walletServiceAddr = builder.Configuration["Wallet:ServiceAddress"] ?? "http://localhost:5005";

// gRPC Client
builder.Services.AddGrpcClient<WalletService.WalletServiceClient>(o =>
{
    o.Address = new Uri(walletServiceAddr);
});

// MassTransit Kafka
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));

    x.AddRider(rider =>
    {
        rider.AddConsumer<WalletCommandConsumer>();
        rider.AddConsumer<UserEventsConsumer>();
        rider.AddConsumer<LoyaltyEventsConsumer>();
        
        rider.AddProducer<WalletResultEvent>("rplus.events.wallet.results.v1");

        rider.UsingKafka((context, k) =>
        {
            k.Host(kafkaBootstrap);

            // Legacy Command Topic
            k.TopicEndpoint<WalletCommandEvent>("rplus.events.wallet.commands.v1", "wallet-adapter-group", e =>
            {
                e.ConfigureConsumer<WalletCommandConsumer>(context);
            });

            // New User Events Topic
            k.TopicEndpoint<EventEnvelope<UserCreated>>("users.user.created.v1", "wallet-adapter-users-group", e =>
            {
                e.ConfigureConsumer<UserEventsConsumer>(context);
            });

            // New Loyalty Events Topic
            k.TopicEndpoint<EventEnvelope<LoyaltyPointsAccrualRequested>>("loyalty.points.accrual.requested.v1", "wallet-adapter-loyalty-group", e =>
            {
                e.ConfigureConsumer<LoyaltyEventsConsumer>(context);
            });
        });
    });
});


var host = builder.Build();
host.Run();
