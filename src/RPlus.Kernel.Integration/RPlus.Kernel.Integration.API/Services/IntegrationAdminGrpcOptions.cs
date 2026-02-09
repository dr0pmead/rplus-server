namespace RPlus.Kernel.Integration.Api.Services;

public sealed class IntegrationAdminGrpcOptions
{
    // Shared secret expected in gRPC metadata: x-rplus-service-secret
    public string? SharedSecret { get; set; }
}

