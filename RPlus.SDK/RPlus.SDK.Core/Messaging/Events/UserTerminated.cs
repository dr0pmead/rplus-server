using System;

namespace RPlus.SDK.Core.Messaging.Events;

/// <summary>
/// Event published when an employee is terminated/dismissed.
/// All services should react by revoking access, deactivating wallets, etc.
/// </summary>
public record UserTerminated(
  Guid EventId,
  DateTime OccurredAt,
  Guid UserId,
  Guid TenantId,
  string Reason,
  string Source);
