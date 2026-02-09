using Microsoft.EntityFrameworkCore;
using RPlus.Loyalty.Domain.Entities;
using RPlus.SDK.Infrastructure.Idempotency;
using RPlus.SDK.Infrastructure.Outbox;

namespace RPlus.Loyalty.Persistence;

public class LoyaltyDbContext : DbContext
{
    public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options)
        : base(options)
    {
    }

    public DbSet<LoyaltyProfile> Profiles => Set<LoyaltyProfile>();
    public DbSet<LoyaltyProgramProfile> ProgramProfiles => Set<LoyaltyProgramProfile>();
    public DbSet<LoyaltyLevel> LoyaltyLevels => Set<LoyaltyLevel>();
    public DbSet<LoyaltyRule> LoyaltyRules => Set<LoyaltyRule>();
    public DbSet<LoyaltyRuleExecution> RuleExecutions => Set<LoyaltyRuleExecution>();
    public DbSet<LoyaltyRuleState> RuleStates => Set<LoyaltyRuleState>();
    public DbSet<LoyaltyGraphRule> GraphRules => Set<LoyaltyGraphRule>();
    public DbSet<LoyaltyGraphRuleExecution> GraphRuleExecutions => Set<LoyaltyGraphRuleExecution>();
    public DbSet<LoyaltyGraphNodeState> GraphNodeStates => Set<LoyaltyGraphNodeState>();
    public DbSet<LoyaltyTenureState> TenureStates => Set<LoyaltyTenureState>();
    public DbSet<LoyaltyIngressEvent> IngressEvents => Set<LoyaltyIngressEvent>();
    public DbSet<LoyaltyScheduledJob> ScheduledJobs => Set<LoyaltyScheduledJob>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<LeaderboardSnapshot> LeaderboardSnapshots => Set<LeaderboardSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoyaltyProfile>(b =>
        {
            b.ToTable("loyalty_profiles");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserId).IsUnique();
            b.Property(x => x.PointsBalance).HasColumnType("numeric(18,2)");
            b.Property(x => x.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<LoyaltyProgramProfile>(b =>
        {
            b.ToTable("loyalty_program_profiles");
            b.HasKey(x => x.UserId);
            b.Property(x => x.Level).HasMaxLength(64);
            b.Property(x => x.TagsJson).HasColumnType("jsonb");
            b.Property(x => x.PointsBalance).HasColumnType("numeric(18,2)");
            b.Property(x => x.Discount).HasColumnType("numeric(18,2)");
            b.Property(x => x.MotivationDiscount).HasColumnType("numeric(18,2)");
            b.HasIndex(x => x.Level);
        });

        modelBuilder.Entity<LoyaltyRule>(b =>
        {
            b.ToTable("loyalty_rules");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.EventType, x.IsActive });
            b.Property(x => x.Points).HasColumnType("numeric(18,2)");
            b.Property(x => x.MetadataFilter).HasColumnType("jsonb");
            b.Property(x => x.RuleType).HasMaxLength(64);
            b.Property(x => x.RuleConfigJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<LoyaltyRuleExecution>(b =>
        {
            b.ToTable("loyalty_rule_executions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OperationId);
            b.HasIndex(x => new { x.OperationId, x.RuleId }).IsUnique();
            b.HasOne<LoyaltyProfile>()
                .WithMany()
                .HasForeignKey(x => x.ProfileId);
            b.HasOne<LoyaltyRule>()
                .WithMany()
                .HasForeignKey(x => x.RuleId);
        });

        modelBuilder.Entity<LoyaltyRuleState>(b =>
        {
            b.ToTable("loyalty_rule_states");
            b.HasKey(x => new { x.RuleId, x.UserId });
            b.Property(x => x.StateJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<LoyaltyGraphRule>(b =>
        {
            b.ToTable("loyalty_graph_rules");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Topic, x.IsActive });
            b.HasIndex(x => x.SystemKey);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Topic).HasMaxLength(200);
            b.Property(x => x.SystemKey).HasMaxLength(200);
            b.Property(x => x.GraphJson).HasColumnType("jsonb");
            b.Property(x => x.VariablesJson).HasColumnType("jsonb");
            b.Property(x => x.MaxExecutions);
            b.Property(x => x.ExecutionsCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<LoyaltyGraphRuleExecution>(b =>
        {
            b.ToTable("loyalty_graph_rule_executions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OperationId);
            b.HasIndex(x => new { x.OperationId, x.RuleId, x.UserId }).IsUnique();
            b.Property(x => x.PointsApplied).HasColumnType("numeric(18,2)");
            b.Property(x => x.OperationId).HasMaxLength(200);
            b.HasOne<LoyaltyGraphRule>()
                .WithMany()
                .HasForeignKey(x => x.RuleId);
        });

        modelBuilder.Entity<LoyaltyGraphNodeState>(b =>
        {
            b.ToTable("loyalty_graph_node_states");
            b.HasKey(x => new { x.RuleId, x.UserId, x.NodeId });
            b.Property(x => x.NodeId).HasMaxLength(64);
            b.Property(x => x.StateJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<LoyaltyTenureState>(b =>
        {
            b.ToTable("loyalty_tenure_state");
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(200);
        });

        modelBuilder.Entity<LoyaltyIngressEvent>(b =>
        {
            b.ToTable("loyalty_ingress_events");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Topic, x.ReceivedAt });
            b.HasIndex(x => new { x.Topic, x.OperationId }).IsUnique();
            b.Property(x => x.Topic).HasMaxLength(200);
            b.Property(x => x.Key).HasMaxLength(200);
            b.Property(x => x.OperationId).HasMaxLength(200);
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.PointsAwarded).HasColumnType("numeric(18,2)");
            b.Property(x => x.ErrorCode).HasMaxLength(128);
        });

        modelBuilder.Entity<LoyaltyScheduledJob>(b =>
        {
            b.ToTable("loyalty_scheduled_jobs");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Status, x.RunAtUtc });
            b.HasIndex(x => new { x.RuleId, x.UserId, x.OperationId }).IsUnique();
            b.Property(x => x.OperationId).HasMaxLength(200);
            b.Property(x => x.EventType).HasMaxLength(200);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Status).HasMaxLength(32);
            b.Property(x => x.LockedBy).HasMaxLength(64);
            b.Property(x => x.LastError).HasMaxLength(2000);
            b.Property(x => x.PointsAwarded).HasColumnType("numeric(18,2)");
            b.HasOne<LoyaltyGraphRule>()
                .WithMany()
                .HasForeignKey(x => x.RuleId);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("loyalty_outbox");
            b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(200);
        });

        modelBuilder.Entity<ProcessedMessage>(b =>
        {
            b.ToTable("loyalty_processed_messages");
            b.HasKey(x => x.MessageId);
        });

        modelBuilder.Entity<LeaderboardSnapshot>(b =>
        {
            b.ToTable("leaderboard_snapshots");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.Year, x.Month }).IsUnique();
            b.HasIndex(x => new { x.Year, x.Month, x.FinalRank });
            b.Property(x => x.RewardType).HasMaxLength(64);
            b.Property(x => x.RewardValue).HasMaxLength(256);
        });

        base.OnModelCreating(modelBuilder);
    }
}
