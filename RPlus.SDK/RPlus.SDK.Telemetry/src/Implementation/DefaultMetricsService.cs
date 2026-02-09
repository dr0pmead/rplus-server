using System;
using System.Collections.Generic;
using System.Diagnostics;
using RPlus.SDK.Telemetry.Abstractions;
using RPlus.SDK.Telemetry.Contracts;

namespace RPlus.SDK.Telemetry.Implementation;

public class DefaultMetricsService : IMetricsService
{
    private readonly ITelemetryPublisher _publisher;
    private readonly IEnumerable<IMetricTagDecorator> _decorators;
    private readonly string _source;
    private readonly string _nodeId;

    public DefaultMetricsService(ITelemetryPublisher publisher, IEnumerable<IMetricTagDecorator> decorators)
    {
        _publisher = publisher;
        _decorators = decorators;
        _source = AppDomain.CurrentDomain.FriendlyName;
        _nodeId = Environment.MachineName;
    }

    public void Increment(string name, double value = 1, Dictionary<string, string>? tags = null)
    {
        Publish(name, value, MetricType.Counter, tags);
    }

    public void Gauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        Publish(name, value, MetricType.Gauge, tags);
    }

    public void Histogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        Publish(name, value, MetricType.Histogram, tags);
    }

    public IDisposable Measure(string name, Dictionary<string, string>? tags = null)
    {
        return new MetricDuration(this, name, tags);
    }

    private void Publish(string name, double value, MetricType type, Dictionary<string, string>? tags)
    {
        var finalTags = tags ?? new Dictionary<string, string>();
        foreach (var decorator in _decorators)
        {
            decorator.Decorate(finalTags);
        }

        var @event = new MetricEvent
        {
            Name = name,
            Value = value,
            Type = type,
            Tags = finalTags,
            Source = _source,
            NodeId = _nodeId,
            Timestamp = DateTime.UtcNow
        };

        _publisher.PublishMetricAsync(@event);
    }

    private class MetricDuration : IDisposable
    {
        private readonly DefaultMetricsService _service;
        private readonly string _name;
        private readonly Dictionary<string, string>? _tags;
        private readonly Stopwatch _stopwatch;

        public MetricDuration(DefaultMetricsService service, string name, Dictionary<string, string>? tags)
        {
            _service = service;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _service.Publish(_name, _stopwatch.Elapsed.TotalMilliseconds, MetricType.Duration, _tags);
        }
    }
}
