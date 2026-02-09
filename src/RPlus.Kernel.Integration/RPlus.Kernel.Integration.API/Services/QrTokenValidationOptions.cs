namespace RPlus.Kernel.Integration.Api.Services;

public sealed class QrTokenValidationOptions
{
    public string Issuer { get; set; } = "RPlus.Auth";

    public string Audience { get; set; } = "RPlus.Kernel";

    public string RequiredType { get; set; } = "qr_login";

    public int ClockSkewSeconds { get; set; } = 5;
}

