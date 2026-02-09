// Decompiled with JetBrains decompiler
// Type: RPlus.Gateway.Infrastructure.Observability.GatewayMetrics
// Assembly: RPlus.Gateway.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 54ABDD44-3C89-45DC-858E-4ECA8F349EB2
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-gateway\RPlus.Gateway.Infrastructure.dll

using RPlus.Gateway.Application.Interfaces.Observability;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

#nullable enable
namespace RPlus.Gateway.Infrastructure.Observability;

public class GatewayMetrics : IGatewayMetrics
{
  private readonly Meter _meter;
  private readonly Counter<long> _requestsTotal;
  private readonly Histogram<double> _requestDuration;
  private readonly Counter<long> _contextCacheHits;
  private readonly Counter<long> _contextCacheMisses;
  private readonly Histogram<double> _contextEnrichmentDuration;
  private readonly Counter<long> _authDecisions;
  private readonly Histogram<double> _authDuration;
  private readonly Counter<long> _stepUpChallenges;
  private readonly Counter<long> _proxyRequests;
  private readonly Histogram<double> _proxyRequestDuration;
  private readonly Counter<long> _kafkaMessages;
  private readonly Histogram<long> _kafkaConsumerLag;
  private readonly Counter<long> _errorsTotal;

  public GatewayMetrics(IMeterFactory meterFactory)
  {
    this._meter = meterFactory.Create("RPlus.Gateway");
    this._requestsTotal = this._meter.CreateCounter<long>("rplus_gateway_requests_total");
    this._requestDuration = this._meter.CreateHistogram<double>("rplus_gateway_request_duration_seconds", "s");
    this._contextCacheHits = this._meter.CreateCounter<long>("rplus_gateway_context_cache_hits_total");
    this._contextCacheMisses = this._meter.CreateCounter<long>("rplus_gateway_context_cache_misses_total");
    this._contextEnrichmentDuration = this._meter.CreateHistogram<double>("rplus_gateway_context_enrichment_duration_seconds", "s");
    this._authDecisions = this._meter.CreateCounter<long>("rplus_gateway_auth_decisions_total");
    this._authDuration = this._meter.CreateHistogram<double>("rplus_gateway_auth_duration_seconds", "s");
    this._stepUpChallenges = this._meter.CreateCounter<long>("rplus_gateway_step_up_challenges_total");
    this._proxyRequests = this._meter.CreateCounter<long>("rplus_gateway_proxy_requests_total");
    this._proxyRequestDuration = this._meter.CreateHistogram<double>("rplus_gateway_proxy_request_duration_seconds", "s");
    this._kafkaMessages = this._meter.CreateCounter<long>("rplus_gateway_kafka_messages_total");
    this._kafkaConsumerLag = this._meter.CreateHistogram<long>("rplus_gateway_kafka_consumer_lag");
    this._errorsTotal = this._meter.CreateCounter<long>("rplus_gateway_errors_total");
  }

  public void RecordRequest(
    string endpoint,
    string method,
    int statusCode,
    double durationSeconds)
  {
    this._requestsTotal.Add(1L, new KeyValuePair<string, object>(nameof (endpoint), (object) endpoint), new KeyValuePair<string, object>(nameof (method), (object) method), new KeyValuePair<string, object>("status_code", (object) statusCode));
    this._requestDuration.Record(durationSeconds, new KeyValuePair<string, object>(nameof (endpoint), (object) endpoint), new KeyValuePair<string, object>(nameof (method), (object) method), new KeyValuePair<string, object>("status_code", (object) statusCode));
  }

  public void RecordError(string route, string errorType)
  {
    this._errorsTotal.Add(1L, new KeyValuePair<string, object>(nameof (route), (object) route), new KeyValuePair<string, object>("error_type", (object) errorType));
  }

  public void RecordContextCacheHit() => this._contextCacheHits.Add(1L);

  public void RecordContextCacheMiss() => this._contextCacheMisses.Add(1L);

  public void RecordContextEnrichmentDuration(double durationSeconds)
  {
    this._contextEnrichmentDuration.Record(durationSeconds);
  }

  public void RecordAuthorizationDecision(string decision)
  {
    this._authDecisions.Add(1L, new KeyValuePair<string, object>(nameof (decision), (object) decision));
  }

  public void RecordAuthorizationDuration(double durationSeconds)
  {
    this._authDuration.Record(durationSeconds);
  }

  public void RecordStepUpChallenge(string reason)
  {
    this._stepUpChallenges.Add(1L, new KeyValuePair<string, object>(nameof (reason), (object) reason));
  }

  public void RecordProxyRequest(string cluster, int statusCode, double durationSeconds)
  {
    this._proxyRequests.Add(1L, new KeyValuePair<string, object>(nameof (cluster), (object) cluster), new KeyValuePair<string, object>("status_code", (object) statusCode));
    this._proxyRequestDuration.Record(durationSeconds, new KeyValuePair<string, object>(nameof (cluster), (object) cluster), new KeyValuePair<string, object>("status_code", (object) statusCode));
  }

  public void RecordKafkaMessage(string topic, string status)
  {
    this._kafkaMessages.Add(1L, new KeyValuePair<string, object>(nameof (topic), (object) topic), new KeyValuePair<string, object>(nameof (status), (object) status));
  }

  public void RecordKafkaConsumerLag(string topic, long lag)
  {
    this._kafkaConsumerLag.Record(lag, new KeyValuePair<string, object>(nameof (topic), (object) topic));
  }
}
