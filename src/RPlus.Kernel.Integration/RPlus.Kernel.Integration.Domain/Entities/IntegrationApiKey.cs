using System;
using System.Collections.Generic;
using System.Linq;
using RPlus.SDK.Integration.Models;

#nullable enable
namespace RPlus.Kernel.Integration.Domain.Entities;

public class IntegrationApiKey : RPlus.SDK.Integration.Models.IntegrationApiKey
{
    public string SecretProtected { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool RequireSignature { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public virtual IntegrationPartner? Partner { get; set; }
    public Dictionary<string, int>? RateLimits { get; set; }

    private IntegrationApiKey()
    {
    }

    public IntegrationApiKey(
        Guid? partnerId,
        string keyHash,
        string secretProtected,
        string prefix,
        string environment,
        IEnumerable<string>? scopes = null,
        IDictionary<string, int>? rateLimits = null,
        DateTime? expiresAt = null,
        bool requireSignature = false)
    {
        this.Id = Guid.NewGuid();
        this.PartnerId = partnerId;
        this.KeyHash = keyHash;
        this.SecretProtected = secretProtected;
        this.Prefix = prefix;
        this.Environment = environment;
        this.Scopes = scopes?.ToList();
        this.RateLimits = rateLimits != null ? new Dictionary<string, int>(rateLimits) : null;
        this.Status = "Active";
        this.CreatedAt = DateTime.UtcNow;
        this.ExpiresAt = expiresAt;
        this.RequireSignature = requireSignature;
    }

    public void Update(string status, bool requireSignature, DateTime? expiresAt)
    {
        this.Status = status;
        this.RequireSignature = requireSignature;
        this.ExpiresAt = expiresAt;
    }

    public void Revoke()
    {
        this.Status = "Revoked";
        this.RevokedAt = DateTime.UtcNow;
    }

    public void Rotate(string newHash, string newSecret)
    {
        this.KeyHash = newHash;
        this.SecretProtected = newSecret;
    }
}
