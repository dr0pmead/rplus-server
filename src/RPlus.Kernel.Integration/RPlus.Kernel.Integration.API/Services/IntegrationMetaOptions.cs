namespace RPlus.Kernel.Integration.Api.Services;

public sealed class IntegrationMetaOptions
{
    public string GrpcAddress { get; set; } = "http://rplus-kernel-meta:5019";
    public string? ServiceSecret { get; set; }
}
