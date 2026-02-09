using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Documents.Infrastructure.Antivirus;

public interface IAntivirusScanner
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken ct);
}

public sealed record ScanResult(bool IsClean, string? RawMessage = null);
