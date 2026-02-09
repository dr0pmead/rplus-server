using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Documents.Infrastructure.Antivirus;

public sealed class ClamAvScanner(AntivirusOptions options, ILogger<ClamAvScanner> logger) : IAntivirusScanner
{
    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(options.Host, options.Port, ct);
        await using var stream = client.GetStream();

        // INSTREAM protocol
        await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), ct);

        var buffer = new byte[8192];
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            var size = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(read);
            var sizeBytes = BitConverter.GetBytes(size);
            await stream.WriteAsync(sizeBytes, ct);
            await stream.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        // zero-length chunk to finish
        await stream.WriteAsync(new byte[4], ct);

        using var response = new MemoryStream();
        read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
        if (read > 0)
        {
            response.Write(buffer, 0, read);
        }

        var message = Encoding.ASCII.GetString(response.ToArray());
        if (message.Contains("FOUND", System.StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("ClamAV detected malware: {Message}", message);
            return new ScanResult(false, message);
        }

        return new ScanResult(true, message);
    }
}
