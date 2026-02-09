using System;
using System.Collections.Generic;

namespace RPlus.Kernel.Integration.Application.Events;

public sealed record IntegrationScanEvent(
    Guid PartnerId,
    Guid KeyId,
    string Environment,
    string QrUserId,
    int StatusCode,
    string Error,
    IReadOnlyCollection<string> Fields,
    DateTime OccurredAtUtc)
{
    public const string EventName = "integration.scan.v1";

    /// <summary>
    /// Scan method used: "qr" for QR code, "otp" for short code.
    /// Default is "qr" for backward compatibility.
    /// </summary>
    public string ScanMethod { get; init; } = "qr";
}
