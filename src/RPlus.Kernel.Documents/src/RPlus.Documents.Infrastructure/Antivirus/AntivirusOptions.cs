namespace RPlus.Documents.Infrastructure.Antivirus;

public sealed class AntivirusOptions
{
    public const string SectionName = "Documents:Antivirus";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public bool FailClosed { get; set; } = true;
}
