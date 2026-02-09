using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using RPlus.Hunter.API.HeadHunter;
using RPlus.Hunter.API.Waba;
using RPlus.SDK.Hunter.Models;

namespace RPlus.Hunter.API.Persistence;

public sealed class HunterDbContext : DbContext
{
    public DbSet<SourcingTaskEntity> SourcingTasks => Set<SourcingTaskEntity>();
    public DbSet<ParsedProfileEntity> ParsedProfiles => Set<ParsedProfileEntity>();
    public DbSet<HhCredential> HhCredentials => Set<HhCredential>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CompanyKnowledge> CompanyKnowledge => Set<CompanyKnowledge>();

    public HunterDbContext(DbContextOptions<HunterDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.Entity<SourcingTaskEntity>(entity =>
        {
            entity.ToTable("sourcing_tasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PositionName).HasColumnName("position_name").HasMaxLength(500).IsRequired();
            entity.Property(e => e.SearchQuery).HasColumnName("search_query").IsRequired();
            entity.Property(e => e.Conditions).HasColumnName("conditions").IsRequired();
            entity.Property(e => e.MessageTemplate).HasColumnName("message_template");
            entity.Property(e => e.DailyContactLimit).HasColumnName("daily_contact_limit").HasDefaultValue(50);
            entity.Property(e => e.MinScore).HasColumnName("min_score").HasDefaultValue(70);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.CandidatesFound).HasColumnName("candidates_found");
            entity.Property(e => e.CandidatesContacted).HasColumnName("candidates_contacted");
            entity.Property(e => e.CandidatesResponded).HasColumnName("candidates_responded");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");

            entity.HasMany(e => e.Profiles)
                  .WithOne()
                  .HasForeignKey(p => p.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParsedProfileEntity>(entity =>
        {
            entity.ToTable("parsed_profiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TaskId).HasColumnName("task_id");
            entity.Property(e => e.ExternalId).HasColumnName("external_id").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
            entity.Property(e => e.RawData).HasColumnName("raw_data").IsRequired();
            entity.Property(e => e.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
            entity.Property(e => e.AiScore).HasColumnName("ai_score");
            entity.Property(e => e.AiVerdict).HasColumnName("ai_verdict");
            entity.Property(e => e.ContactPhone).HasColumnName("contact_phone").HasMaxLength(30);
            entity.Property(e => e.TelegramHandle).HasColumnName("telegram_handle").HasMaxLength(100);
            entity.Property(e => e.ContactEmail).HasColumnName("contact_email").HasMaxLength(200);
            entity.Property(e => e.PreferredChannel).HasColumnName("preferred_channel").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.ParsedAt).HasColumnName("parsed_at");
            entity.Property(e => e.ContactedAt).HasColumnName("contacted_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.ConversationMode).HasColumnName("conversation_mode").HasMaxLength(20).HasDefaultValue("AI_AUTO");

            // Smart dedup index: unique per task + external source ID
            entity.HasIndex(e => new { e.TaskId, e.ExternalId })
                  .IsUnique()
                  .HasDatabaseName("ix_parsed_profiles_task_external");

            // Index for worker queries
            entity.HasIndex(e => new { e.TaskId, e.Status })
                  .HasDatabaseName("ix_parsed_profiles_task_status");

            // Contact phone dedup index: prevent messaging same person twice across tasks
            entity.HasIndex(e => e.ContactPhone)
                  .HasDatabaseName("ix_parsed_profiles_contact_phone")
                  .HasFilter("contact_phone IS NOT NULL");
        });

        modelBuilder.Entity<HhCredential>(entity =>
        {
            entity.ToTable("hh_credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccessToken).HasColumnName("access_token").IsRequired();
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token").IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ProfileId).HasColumnName("profile_id");
            entity.Property(e => e.Direction).HasColumnName("direction").HasMaxLength(20).IsRequired();
            entity.Property(e => e.SenderType).HasColumnName("sender_type").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.WabaMessageId).HasColumnName("waba_message_id").HasMaxLength(200);
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("sent");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.ProfileId)
                  .HasDatabaseName("idx_chat_profile");

            entity.HasIndex(e => e.WabaMessageId)
                  .HasDatabaseName("idx_chat_waba_message_id")
                  .HasFilter("waba_message_id IS NOT NULL");
        });

        modelBuilder.Entity<CompanyKnowledge>(entity =>
        {
            entity.ToTable("company_knowledge");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.Category)
                  .HasDatabaseName("ix_company_knowledge_category");
        });
    }
}

/// <summary>
/// EF entity inheriting from SDK model.
/// </summary>
public class SourcingTaskEntity : SourcingTask
{
    public ICollection<ParsedProfileEntity> Profiles { get; set; } = new List<ParsedProfileEntity>();
}

/// <summary>
/// EF entity inheriting from SDK model.
/// </summary>
public class ParsedProfileEntity : ParsedProfile
{
}
