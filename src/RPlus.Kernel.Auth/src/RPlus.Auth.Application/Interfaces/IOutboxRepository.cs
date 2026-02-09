// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.IOutboxRepository
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using RPlus.Auth.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface IOutboxRepository
{
  Task AddAsync(OutboxMessageEntity message, CancellationToken cancellationToken = default (CancellationToken));

  Task<List<OutboxMessageEntity>> ClaimMessagesAsync(
    int batchSize,
    CancellationToken cancellationToken = default (CancellationToken));

  Task ReleaseExpiredLeasesAsync(CancellationToken cancellationToken = default (CancellationToken));

  Task MarkAsSentAsync(Guid id, CancellationToken cancellationToken = default (CancellationToken));

  Task MarkAsFailedAsync(
    Guid id,
    string error,
    string? stackTrace,
    CancellationToken cancellationToken = default (CancellationToken));
}
