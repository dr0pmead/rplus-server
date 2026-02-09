namespace RPlus.Kernel.Integration.Api.Services;

public sealed class IntegrationScanOptions
{
    public string ApiKeyHeaderName { get; set; } = "X-Integration-Key";

    public string RequiredPermission { get; set; } = "integration.scan";

    public bool EnforcePermission { get; set; } = false;

    public string SignatureHeaderName { get; set; } = "X-Integration-Signature";

    public string SignatureTimestampHeaderName { get; set; } = "X-Integration-Timestamp";

    public int SignatureToleranceSeconds { get; set; } = 300;

    public int MpVisitTtlHours { get; set; } = 12;

    public int QrTokenReplayTtlSeconds { get; set; } = 120;

    public int UsersProfileTimeoutMs { get; set; } = 400;

    public bool AllowPartialResponse { get; set; } = true;

    public bool DevIssuerEnabled { get; set; } = false;

    public int DevIssuerTokenTtlSeconds { get; set; } = 60;
}
