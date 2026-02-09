using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using RPlus.SDK.Core.Abstractions;

namespace RPlus.SDK.Infrastructure.Services;

public sealed class RPlusMetricsService : IRPlusMetrics
{
    private readonly Meter _meter;
    private readonly string _moduleId;
    private readonly Dictionary<string, Counter<long>> _counters = new();
    private readonly Dictionary<string, Histogram<double>> _histograms = new();

    public RPlusMetricsService(IModuleManifest manifest)
    {
        _moduleId = manifest.ModuleId;
        _meter = new Meter(_moduleId, manifest.ModuleVersion.ToString());
    }

    public void IncrementCounter(string name, params (string Key, string Value)[] tags)
    {
        var counter = GetOrCreateCounter(name);
        counter.Add(1, BuildTags(tags));
    }

    public void RecordGauge(string name, double value, params (string Key, string Value)[] tags)
    {
        // There is no dedicated gauge instrument exposed via IMetrics yet, so treat it as histogram sample.
        RecordHistogram(name, value, tags);
    }

    public void RecordHistogram(string name, double value, params (string Key, string Value)[] tags)
    {
        var histogram = GetOrCreateHistogram(name);
        histogram.Record(value, BuildTags(tags));
    }

    private Counter<long> GetOrCreateCounter(string name)
    {
        if (_counters.TryGetValue(name, out var counter))
        {
            return counter;
        }

        counter = _meter.CreateCounter<long>(name);
        _counters[name] = counter;
        return counter;
    }

    private Histogram<double> GetOrCreateHistogram(string name)
    {
        if (_histograms.TryGetValue(name, out var histogram))
        {
            return histogram;
        }

        histogram = _meter.CreateHistogram<double>(name);
        _histograms[name] = histogram;
        return histogram;
    }

    private KeyValuePair<string, object?>[] BuildTags(ReadOnlySpan<(string Key, string Value)> tags)
    {
        var result = new KeyValuePair<string, object?>[tags.Length + 1];
        result[0] = new KeyValuePair<string, object?>("module_id", _moduleId);

        for (var i = 0; i < tags.Length; i++)
        {
            result[i + 1] = new KeyValuePair<string, object?>(tags[i].Key, tags[i].Value);
        }

        return result;
    }
}
