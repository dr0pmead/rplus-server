namespace RPlus.Access.Api.Services;

public sealed class IntegrationAdminClientOptions
{
    // Optional shared secret sent to Integration gRPC admin: x-rplus-service-secret
    public string? SharedSecret { get; set; }
}

