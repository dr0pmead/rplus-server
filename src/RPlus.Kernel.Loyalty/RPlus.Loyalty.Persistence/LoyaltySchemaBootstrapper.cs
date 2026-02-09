using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Loyalty.Persistence;

public static class LoyaltySchemaBootstrapper
{
    public static async Task ApplyAsync(LoyaltyDbContext db, CancellationToken ct = default)
    {
        // EnsureCreated only creates schema for empty databases. In dev environments where the DB already exists,
        // we still need a minimal, safe bootstrap to keep the loyalty engine running without manual intervention.

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_rule_states (
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "StateJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_loyalty_rule_states" PRIMARY KEY ("RuleId", "UserId")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_program_profiles (
              "UserId" uuid NOT NULL,
              "Level" text NULL,
              "TagsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
              "PointsBalance" numeric(18,2) NOT NULL DEFAULT 0,
              "Discount" numeric(18,2) NOT NULL DEFAULT 0,
              "MotivationDiscount" numeric(18,2) NOT NULL DEFAULT 0,
              "CreatedAtUtc" timestamptz NOT NULL DEFAULT now(),
              "UpdatedAtUtc" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_loyalty_program_profiles" PRIMARY KEY ("UserId")
            );

            CREATE INDEX IF NOT EXISTS "IX_loyalty_program_profiles_Level"
              ON loyalty_program_profiles ("Level");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE IF EXISTS loyalty_program_profiles
              ADD COLUMN IF NOT EXISTS "Discount" numeric(18,2) NOT NULL DEFAULT 0;

            ALTER TABLE IF EXISTS loyalty_program_profiles
              ADD COLUMN IF NOT EXISTS "MotivationDiscount" numeric(18,2) NOT NULL DEFAULT 0;
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_ingress_events (
              "Id" uuid NOT NULL,
              "Topic" text NOT NULL,
              "Key" text NOT NULL,
              "OperationId" text NOT NULL,
              "EventType" text NULL,
              "UserId" uuid NOT NULL,
              "OccurredAt" timestamptz NOT NULL,
              "ReceivedAt" timestamptz NOT NULL DEFAULT now(),
              "PayloadJson" jsonb NOT NULL,
              "ProcessedAt" timestamptz NULL,
              "PointsAwarded" numeric(18,2) NOT NULL DEFAULT 0,
              "ErrorCode" text NULL,
              "ErrorMessage" text NULL,
              CONSTRAINT "PK_loyalty_ingress_events" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_loyalty_ingress_events_Topic_OperationId"
              ON loyalty_ingress_events ("Topic", "OperationId");

            CREATE INDEX IF NOT EXISTS "IX_loyalty_ingress_events_Topic_ReceivedAt"
              ON loyalty_ingress_events ("Topic", "ReceivedAt");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_graph_rules (
              "Id" uuid NOT NULL,
              "Name" text NOT NULL,
              "Topic" text NOT NULL,
              "Priority" integer NOT NULL DEFAULT 100,
              "IsActive" boolean NOT NULL DEFAULT true,
              "MaxExecutions" integer NULL,
              "ExecutionsCount" integer NOT NULL DEFAULT 0,
              "IsSystem" boolean NOT NULL DEFAULT false,
              "SystemKey" text NULL,
              "GraphJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "VariablesJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "Description" text NULL,
              "CreatedAt" timestamptz NOT NULL DEFAULT now(),
              "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_loyalty_graph_rules" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_loyalty_graph_rules_Topic_IsActive"
              ON loyalty_graph_rules ("Topic", "IsActive");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE IF EXISTS loyalty_graph_rules
              ADD COLUMN IF NOT EXISTS "MaxExecutions" integer NULL;

            ALTER TABLE IF EXISTS loyalty_graph_rules
              ADD COLUMN IF NOT EXISTS "ExecutionsCount" integer NOT NULL DEFAULT 0;

            ALTER TABLE IF EXISTS loyalty_graph_rules
              ADD COLUMN IF NOT EXISTS "IsSystem" boolean NOT NULL DEFAULT false;

            ALTER TABLE IF EXISTS loyalty_graph_rules
              ADD COLUMN IF NOT EXISTS "SystemKey" text NULL;

            ALTER TABLE IF EXISTS loyalty_graph_rules
              ADD COLUMN IF NOT EXISTS "VariablesJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
            """,
            ct);

        // Create index on SystemKey only after column exists (for existing DBs).
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'loyalty_graph_rules'
                      AND column_name = 'SystemKey'
                  ) THEN
                    EXECUTE 'CREATE INDEX IF NOT EXISTS "IX_loyalty_graph_rules_SystemKey" ON loyalty_graph_rules ("SystemKey")';
                  END IF;
                END $$;
                """,
                ct);
        }
        catch
        {
            // ignore: schema might be managed externally
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_graph_rule_executions (
              "Id" uuid NOT NULL,
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "OperationId" text NOT NULL,
              "PointsApplied" numeric(18,2) NOT NULL DEFAULT 0,
              "CreatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_loyalty_graph_rule_executions" PRIMARY KEY ("Id"),
              CONSTRAINT "FK_loyalty_graph_rule_executions_RuleId" FOREIGN KEY ("RuleId")
                REFERENCES loyalty_graph_rules("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_loyalty_graph_rule_executions_OperationId"
              ON loyalty_graph_rule_executions ("OperationId");
            """,
            ct);

        // Replace old unique index (OperationId, RuleId) with (OperationId, RuleId, UserId) to support audience iteration.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM pg_indexes
                    WHERE schemaname = 'public'
                      AND tablename = 'loyalty_graph_rule_executions'
                      AND indexname = 'UX_loyalty_graph_rule_executions_OperationId_RuleId'
                  ) THEN
                    EXECUTE 'DROP INDEX IF EXISTS "UX_loyalty_graph_rule_executions_OperationId_RuleId"';
                  END IF;
                END $$;
                """,
                ct);
        }
        catch
        {
            // ignore: schema might be managed externally
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_loyalty_graph_rule_executions_OperationId_RuleId_UserId"
              ON loyalty_graph_rule_executions ("OperationId", "RuleId", "UserId");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_graph_node_states (
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "NodeId" text NOT NULL,
              "StateJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_loyalty_graph_node_states" PRIMARY KEY ("RuleId", "UserId", "NodeId")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_tenure_state (
              "Key" text NOT NULL,
              "LevelsHash" text NULL,
              "LastRunAtUtc" timestamptz NULL,
              CONSTRAINT "PK_loyalty_tenure_state" PRIMARY KEY ("Key")
            );
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS loyalty_scheduled_jobs (
              "Id" uuid NOT NULL,
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "RunAtUtc" timestamptz NOT NULL,
              "OperationId" text NOT NULL,
              "EventType" text NULL,
              "PayloadJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "Status" text NOT NULL DEFAULT 'Pending',
              "LockedUntilUtc" timestamptz NULL,
              "LockedBy" text NULL,
              "Attempts" integer NOT NULL DEFAULT 0,
              "LastError" text NULL,
              "PointsAwarded" numeric(18,2) NOT NULL DEFAULT 0,
              "CreatedAtUtc" timestamptz NOT NULL DEFAULT now(),
              "UpdatedAtUtc" timestamptz NOT NULL DEFAULT now(),
              "CompletedAtUtc" timestamptz NULL,
              CONSTRAINT "PK_loyalty_scheduled_jobs" PRIMARY KEY ("Id"),
              CONSTRAINT "FK_loyalty_scheduled_jobs_RuleId" FOREIGN KEY ("RuleId")
                REFERENCES loyalty_graph_rules("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_loyalty_scheduled_jobs_Status_RunAtUtc"
              ON loyalty_scheduled_jobs ("Status", "RunAtUtc");

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_loyalty_scheduled_jobs_RuleId_UserId_OperationId"
              ON loyalty_scheduled_jobs ("RuleId", "UserId", "OperationId");
            """,
            ct);

        // Replace the old unique index on OperationId with a composite unique index.
        // Best-effort: if the old index doesn't exist, these statements are no-ops.
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1
                    FROM pg_indexes
                    WHERE schemaname = 'public'
                      AND tablename = 'loyalty_rule_executions'
                      AND indexname = 'IX_loyalty_rule_executions_OperationId'
                  ) THEN
                    EXECUTE 'DROP INDEX IF EXISTS "IX_loyalty_rule_executions_OperationId"';
                  END IF;
                END $$;
                """,
                ct);
        }
        catch
        {
            // ignore: schema might be managed externally
        }

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_loyalty_rule_executions_OperationId"
              ON loyalty_rule_executions ("OperationId");

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_loyalty_rule_executions_OperationId_RuleId"
              ON loyalty_rule_executions ("OperationId", "RuleId");
            """,
            ct);

        // Leaderboard snapshots for reward distribution
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS leaderboard_snapshots (
              "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
              "UserId" uuid NOT NULL,
              "Year" integer NOT NULL,
              "Month" integer NULL,
              "FinalRank" integer NOT NULL,
              "FinalPoints" bigint NOT NULL,
              "RewardType" text NULL,
              "RewardValue" text NULL,
              "RewardDistributed" boolean NOT NULL DEFAULT false,
              "SnapshotAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_leaderboard_snapshots" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_leaderboard_snapshots_UserId_Year_Month"
              ON leaderboard_snapshots ("UserId", "Year", "Month");

            CREATE INDEX IF NOT EXISTS "IX_leaderboard_snapshots_Year_Month_FinalRank"
              ON leaderboard_snapshots ("Year", "Month", "FinalRank");
            """,
            ct);
    }
}
