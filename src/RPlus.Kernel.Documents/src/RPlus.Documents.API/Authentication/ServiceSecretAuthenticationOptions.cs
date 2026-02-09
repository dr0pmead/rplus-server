namespace RPlus.Documents.Api.Authentication;

public sealed class ServiceSecretAuthenticationOptions
{
    public string HeaderName { get; set; } = "x-rplus-service-secret";

    public string SharedSecret { get; set; } = string.Empty;
}
