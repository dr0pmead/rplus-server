using System;

namespace RPlus.Access.Domain.Entities;

public sealed class IntegrationApiKeyRecord
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public App? Application { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Environment { get; set; } = "live";

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}

