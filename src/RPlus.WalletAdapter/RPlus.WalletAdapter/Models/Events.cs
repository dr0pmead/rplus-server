using System;
using System.Collections.Generic;

namespace RPlus.WalletAdapter.Models;

public class WalletCommandEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "wallet.command";
    public string EventVersion { get; set; } = "v1";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public EventSource Source { get; set; } = new();
    public EventActor Actor { get; set; } = new();
    public EventSubject Subject { get; set; } = new();
    public WalletCommandContext Context { get; set; } = new();
    public Guid CorrelationId { get; set; }
    public Guid TraceId { get; set; }
}

public class EventSource
{
    public string Service { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
}

public class EventActor
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class EventSubject
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class WalletCommandContext
{
    public string Command { get; set; } = string.Empty; // accrue, reserve, commit, cancel, reverse
    public string UserId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class WalletResultEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty; // wallet.command.executed, wallet.command.failed
    public string EventVersion { get; set; } = "v1";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid CorrelationId { get; set; }
    public WalletResultContext Context { get; set; } = new();
}

public class WalletResultContext
{
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public long BalanceAfter { get; set; }
    public string OperationId { get; set; } = string.Empty;
}
