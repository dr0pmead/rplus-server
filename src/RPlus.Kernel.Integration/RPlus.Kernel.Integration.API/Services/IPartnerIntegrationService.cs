using System;
using System.Threading;
using System.Threading.Tasks;
using RPlus.Kernel.Integration.Api.Controllers;
using RPlus.Kernel.Integration.Api.Models.Partners;

namespace RPlus.Kernel.Integration.Api.Services;

/// <summary>
/// Partner integration service for Intent → Commit → Cancel flow.
/// Handles idempotency and atomic transactions.
/// </summary>
public interface IPartnerIntegrationService
{
    /// <summary>
    /// Process a scan intent (registers predicted discounts).
    /// Idempotent: returns same result for same (partnerId, idempotencyKey).
    /// </summary>
    Task<PartnerScanResponse> ProcessScanAsync(
        Guid partnerId,
        string idempotencyKey,
        PartnerScanRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Process order commit (locks financial facts).
    /// Idempotent: returns same CommitId for same ScanId.
    /// </summary>
    Task<PartnerCommitResponse> ProcessCommitAsync(
        Guid partnerId,
        PartnerCommitRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel a scan intent (order cancelled/storned on POS).
    /// Updates scan status to Cancelled.
    /// </summary>
    Task ProcessCancelAsync(
        Guid partnerId,
        PartnerCancelRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Process a telemetry event from a POS plugin.
    /// Best-effort: should not throw under normal conditions.
    /// </summary>
    Task ProcessEventAsync(
        Guid partnerId,
        string idempotencyKey,
        PartnerEventRequest request,
        CancellationToken ct = default);
}
