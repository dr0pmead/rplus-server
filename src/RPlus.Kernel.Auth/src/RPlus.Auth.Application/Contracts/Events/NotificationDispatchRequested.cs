using System;

namespace RPlus.Auth.Application.Contracts.Events;

public sealed record NotificationDispatchRequested(
    string AggregateId,
    string Channel,
    string Title,
    string Body,
    string Recipient,
    DateTime CreatedAtUtc);

