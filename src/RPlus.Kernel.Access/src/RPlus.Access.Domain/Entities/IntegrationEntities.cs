using System;

#nullable enable

namespace RPlus.Access.Domain.Entities;

public class IntegrationApiKeyPermission
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public string PermissionId { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
}
