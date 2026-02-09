namespace RPlus.Documents.Api.Options;

public sealed class DocumentUploadOptions
{
    public const string SectionName = "Documents:Upload";

    public long MaxDocumentBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxMediaBytes { get; set; } = 100 * 1024 * 1024;

    public string[] AllowedDocumentExtensions { get; set; } =
    [
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".txt"
    ];

    public string[] AllowedImageExtensions { get; set; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp",
        ".heic"
    ];

    public string[] AllowedVideoExtensions { get; set; } =
    [
        ".mp4",
        ".mov",
        ".avi",
        ".mkv",
        ".webm"
    ];

    public string[] AllowedDocumentMimeTypes { get; set; } =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain"
    ];

    public string[] AllowedImageMimeTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/bmp",
        "image/heic"
    ];

    public string[] AllowedVideoMimeTypes { get; set; } =
    [
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo",
        "video/x-matroska",
        "video/webm"
    ];
}
