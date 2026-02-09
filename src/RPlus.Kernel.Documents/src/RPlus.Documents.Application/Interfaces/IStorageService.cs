using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.Documents.Application.Interfaces;

public interface IStorageService
{
    Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream?> DownloadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
