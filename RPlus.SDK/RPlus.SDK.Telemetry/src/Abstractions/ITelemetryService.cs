using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPlus.SDK.Telemetry.Abstractions;

public interface ITelemetryService
{
    Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null);
    Task TrackSystemHealthAsync(string component, string status, string? message = null);
    Task TrackTechnicalAuditAsync(string action, string resource, Dictionary<string, object>? metadata = null);
}
