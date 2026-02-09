using System;

namespace RPlus.Access.Domain.Entities;

public sealed class PartnerUserLink
{
    public Guid ApplicationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

