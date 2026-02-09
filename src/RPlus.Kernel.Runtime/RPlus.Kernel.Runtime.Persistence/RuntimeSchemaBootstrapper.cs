using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Kernel.Runtime.Persistence;

public static class RuntimeSchemaBootstrapper
{
    public static async Task ApplyAsync(RuntimeDbContext db, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS runtime;", ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS runtime.graph_executions (
              "Id" uuid NOT NULL,
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "OperationId" text NOT NULL,
              "Matched" boolean NOT NULL DEFAULT false,
              "PointsDelta" numeric(18,2) NOT NULL DEFAULT 0,
              "ActionsJson" jsonb NOT NULL DEFAULT '[]'::jsonb,
              "OccurredAt" timestamptz NOT NULL,
              "CreatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_runtime_graph_executions" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_runtime_graph_executions_rule_user_operation"
              ON runtime.graph_executions ("RuleId", "UserId", "OperationId");

            CREATE INDEX IF NOT EXISTS "IX_runtime_graph_executions_created_at"
              ON runtime.graph_executions ("CreatedAt");
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS runtime.graph_node_states (
              "RuleId" uuid NOT NULL,
              "UserId" uuid NOT NULL,
              "NodeId" text NOT NULL,
              "StateJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb,
              "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
              CONSTRAINT "PK_runtime_graph_node_states" PRIMARY KEY ("RuleId", "UserId", "NodeId")
            );
            """,
            ct);
    }
}
