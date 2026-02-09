using System;
using System.Collections.Generic;

namespace RPlus.SDK.Telemetry.Abstractions;

public interface IMetricsService
{
    void Increment(string name, double value = 1, Dictionary<string, string>? tags = null);
    void Gauge(string name, double value, Dictionary<string, string>? tags = null);
    void Histogram(string name, double value, Dictionary<string, string>? tags = null);
    IDisposable Measure(string name, Dictionary<string, string>? tags = null);
}

public interface IMetricTagDecorator
{
    void Decorate(Dictionary<string, string> tags);
}
