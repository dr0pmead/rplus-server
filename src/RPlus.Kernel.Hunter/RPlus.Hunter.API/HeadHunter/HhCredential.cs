namespace RPlus.Hunter.API.HeadHunter;

/// <summary>
/// Stored OAuth2 credentials for HeadHunter API.
/// Persisted in DB so HarvesterWorker survives restarts without re-auth.
/// </summary>
public class HhCredential
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
