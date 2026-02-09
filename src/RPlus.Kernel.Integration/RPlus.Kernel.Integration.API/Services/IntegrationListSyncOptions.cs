namespace RPlus.Kernel.Integration.Api.Services;

public sealed class IntegrationListSyncOptions
{
    public string RequiredPermission { get; set; } = "integration.list.sync";
    public bool EnforcePermission { get; set; } = true;
    public int MaxItems { get; set; } = 1000;
    public int MaxPayloadBytes { get; set; } = 1_000_000;
}
