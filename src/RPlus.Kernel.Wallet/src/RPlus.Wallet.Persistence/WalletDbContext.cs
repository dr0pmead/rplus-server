using Microsoft.EntityFrameworkCore;
using RPlus.SDK.Infrastructure.Outbox;
using RPlus.SDK.Infrastructure.Idempotency;
using WalletEntity = RPlus.Wallet.Domain.Entities.Wallet;
using WalletTransactionEntity = RPlus.Wallet.Domain.Entities.WalletTransaction;

namespace RPlus.Wallet.Persistence;

public class WalletDbContext : DbContext
{
    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<WalletTransactionEntity> WalletTransactions => Set<WalletTransactionEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WalletEntity>(b =>
        {
            b.ToTable("wallets");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserId).IsUnique();
            b.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<WalletTransactionEntity>(b =>
        {
            b.ToTable("transactions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OperationId).IsUnique();
            b.HasIndex(x => x.UserId);
            // Index for monthly motivational points aggregation
            b.HasIndex(x => new { x.UserId, x.Year, x.Month, x.SourceType })
                .HasDatabaseName("ix_transactions_user_month_source");
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("processed_messages");
            b.HasKey(x => x.MessageId);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
