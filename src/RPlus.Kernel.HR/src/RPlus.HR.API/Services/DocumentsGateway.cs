using Microsoft.Net.Http.Headers;
using System.Net.Http.Json;

namespace RPlus.HR.Api.Services;

public sealed class DocumentsGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentsGateway> _logger;

    public DocumentsGateway(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DocumentsGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid?> EnsureUserFolderAsync(Guid userId, CancellationToken ct)
    {
        var client = CreateClient();
        if (client == null)
            return null;

        try
        {
            var list = await client.GetFromJsonAsync<DocumentFolderDto[]>(
                $"/api/documents/folders?ownerUserId={userId:D}&type=User",
                cancellationToken: ct);

            var existing = list?.FirstOrDefault();
            if (existing != null)
                return existing.Id;

            var res = await client.PostAsJsonAsync(
                "/api/documents/folders",
                new
                {
                    name = "User Documents",
                    type = "User",
                    ownerUserId = userId,
                    isSystem = false,
                    isImmutable = true
                },
                ct);

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Documents folder create failed for user {UserId}. Status={Status}. Body={Body}", userId, (int)res.StatusCode, body);
                return null;
            }

            var created = await res.Content.ReadFromJsonAsync<DocumentFolderDto>(cancellationToken: ct);
            return created?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Documents folder ensure failed for user {UserId}", userId);
            return null;
        }
    }

    public async Task<Guid?> UploadFileAsync(
        Guid ownerUserId,
        Guid folderId,
        IFormFile file,
        bool isLocked,
        string? documentType,
        string? subjectType,
        Guid? subjectId,
        CancellationToken ct)
    {
        var client = CreateClient();
        if (client == null)
            return null;

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            content.Add(fileContent, "file", file.FileName);
            content.Add(new StringContent(file.FileName ?? string.Empty), "fileName");
            content.Add(new StringContent(file.ContentType ?? "application/octet-stream"), "contentType");
            content.Add(new StringContent(ownerUserId.ToString("D")), "ownerUserId");
            content.Add(new StringContent(folderId.ToString("D")), "folderId");
            content.Add(new StringContent(isLocked ? "true" : "false"), "isLocked");

            if (!string.IsNullOrWhiteSpace(documentType))
                content.Add(new StringContent(documentType), "documentType");
            if (!string.IsNullOrWhiteSpace(subjectType))
                content.Add(new StringContent(subjectType), "subjectType");
            if (subjectId.HasValue)
                content.Add(new StringContent(subjectId.Value.ToString("D")), "subjectId");

            var res = await client.PostAsync("/api/documents/files", content, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Documents upload failed for user {UserId}. Status={Status}. Body={Body}", ownerUserId, (int)res.StatusCode, body);
                return null;
            }

            var uploaded = await res.Content.ReadFromJsonAsync<DocumentFileDto>(cancellationToken: ct);
            return uploaded?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Documents upload failed for user {UserId}", ownerUserId);
            return null;
        }
    }

    public async Task<Stream?> DownloadFileAsync(Guid documentId, CancellationToken ct)
    {
        var client = CreateClient();
        if (client == null)
            return null;

        try
        {
            var res = await client.GetAsync($"/api/documents/files/{documentId:D}/download", HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
                return null;

            return await res.Content.ReadAsStreamAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Documents download failed for document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(Guid documentId, CancellationToken ct)
    {
        var client = CreateClient();
        if (client == null)
            return false;

        try
        {
            var res = await client.DeleteAsync($"/api/documents/files/{documentId:D}", ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Documents delete failed for document {DocumentId}. Status={Status}. Body={Body}", documentId, (int)res.StatusCode, body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Documents delete failed for document {DocumentId}", documentId);
            return false;
        }
    }

    private HttpClient? CreateClient()
    {
        var baseAddress = _configuration["Services:Documents:Http"]
                          ?? _configuration["Services__Documents__Http"]
                          ?? _configuration["Documents:Http:BaseAddress"]
                          ?? _configuration["Documents__Http__BaseAddress"]
                          ?? "http://rplus-kernel-documents:5017";

        var internalSecret =
            _configuration["Documents:Internal:SharedSecret"]
            ?? _configuration["Documents__Internal__SharedSecret"]
            ?? _configuration["RPLUS_INTERNAL_SERVICE_SECRET"];

        var serviceSecret =
            _configuration["Documents:Auth:SharedSecret"]
            ?? _configuration["Documents__Auth__SharedSecret"]
            ?? _configuration["DOCUMENTS_AUTH_SHARED_SECRET"];

        if (string.IsNullOrWhiteSpace(serviceSecret))
            serviceSecret = internalSecret;

        if (string.IsNullOrWhiteSpace(internalSecret) || string.IsNullOrWhiteSpace(serviceSecret))
        {
            _logger.LogWarning("DocumentsGateway disabled: missing Documents:Internal:SharedSecret or Documents:Auth:SharedSecret");
            return null;
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseAddress.TrimEnd('/'));
        client.DefaultRequestHeaders.Remove("X-RPlus-Internal");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-RPlus-Internal", internalSecret);
        client.DefaultRequestHeaders.Remove("x-rplus-service-secret");
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-rplus-service-secret", serviceSecret);
        client.DefaultRequestHeaders.Remove(HeaderNames.Accept);
        client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        return client;
    }

    private sealed record DocumentFolderDto(Guid Id, string Name, string Type);

    private sealed record DocumentFileDto(Guid Id);
}
