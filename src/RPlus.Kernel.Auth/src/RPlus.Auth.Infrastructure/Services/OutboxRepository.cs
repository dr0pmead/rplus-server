// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Services.OutboxRepository
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using RPlus.Auth.Application.Interfaces;
using RPlus.Auth.Domain.Entities;
using RPlus.Auth.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Infrastructure.Services;

public sealed class OutboxRepository : IOutboxRepository
{
  private readonly AuthDbContext _db;

  public OutboxRepository(AuthDbContext db) => this._db = db;

  public async Task AddAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default (CancellationToken))
  {
    this._db.OutboxMessages.Add(message);
    await Task.CompletedTask;
  }

  public async Task<List<OutboxMessageEntity>> ClaimMessagesAsync(
    int batchSize,
    CancellationToken cancellationToken = default (CancellationToken))
  {
    string str1 = "Pending";
    string str2 = "Failed";
    string str3 = "Processing";
    DateTime utcNow = DateTime.UtcNow;
    DateTime dateTime = utcNow.AddMinutes(5.0);
    string machineName = Environment.MachineName;
    return await this._db.OutboxMessages.FromSqlRaw<OutboxMessageEntity>("\n            UPDATE \"auth\".\"outbox_messages\"\n            SET status = {0}, processed_at = {1}, locked_by = {2}, locked_until = {3}\n            WHERE \"id\" IN (\n                SELECT \"id\"\n                FROM \"auth\".\"outbox_messages\"\n                WHERE (status = {4})\n                   OR (status = {5} AND retry_count < max_retries AND next_retry_at <= {6})\n                   OR (status = {7} AND locked_until < {8})\n                ORDER BY created_at\n                LIMIT {9}\n                FOR UPDATE SKIP LOCKED\n            )\n            RETURNING *;\n        ", (object) str3, (object) utcNow, (object) machineName, (object) dateTime, (object) str1, (object) str2, (object) utcNow, (object) str3, (object) utcNow, (object) batchSize).ToListAsync<OutboxMessageEntity>(cancellationToken);
  }

  public async Task ReleaseExpiredLeasesAsync(CancellationToken cancellationToken = default (CancellationToken))
  {
    DateTime now = DateTime.UtcNow;
    string processingStatus = "Processing";
    string pendingStatus = "Pending";
    int num = await this._db.OutboxMessages.Where(m => m.Status == processingStatus && m.LockedUntil < now)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(m => m.Status, pendingStatus)
            .SetProperty(m => m.LockedBy, (string?)null)
            .SetProperty(m => m.LockedUntil, (DateTime?)null), cancellationToken);
  }

  public async Task MarkAsSentAsync(Guid id, CancellationToken cancellationToken = default (CancellationToken))
  {
    OutboxMessageEntity async = await this._db.OutboxMessages.FindAsync(new object[1]
    {
      (object) id
    }, cancellationToken);
    if (async == null)
      return;
    async.Status = "Processed";
    async.SentAt = new DateTime?(DateTime.UtcNow);
    async.ProcessedAt = new DateTime?(DateTime.UtcNow);
    int num = await this._db.SaveChangesAsync(cancellationToken);
  }

  public async Task MarkAsFailedAsync(
    Guid id,
    string error,
    string? stackTrace,
    CancellationToken cancellationToken = default (CancellationToken))
  {
    OutboxMessageEntity async = await this._db.OutboxMessages.FindAsync(new object[1]
    {
      (object) id
    }, cancellationToken);
    if (async == null)
      return;
    ++async.RetryCount;
    async.ErrorMessage = error;
    async.ErrorStackTrace = stackTrace;
    async.ProcessedAt = new DateTime?(DateTime.UtcNow);
    if (async.RetryCount >= async.MaxRetries)
    {
      async.Status = "DeadLetter";
    }
    else
    {
      async.Status = "Failed";
      async.NextRetryAt = new DateTime?(DateTime.UtcNow.AddSeconds(Math.Pow(10.0, (double) async.RetryCount)));
    }
    int num = await this._db.SaveChangesAsync(cancellationToken);
  }
}
