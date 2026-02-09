namespace RPlus.Gateway.Application.Interfaces.Observability;

public interface IGatewayMetrics
{
    void RecordRequest(string route, string method, int statusCode, double durationSeconds);
    void RecordError(string route, string errorType);
    void RecordContextCacheHit();
    void RecordContextCacheMiss();
    void RecordContextEnrichmentDuration(double durationSeconds);
    void RecordAuthorizationDecision(string decision);
    void RecordAuthorizationDuration(double durationSeconds);
    void RecordStepUpChallenge(string reason);
    void RecordProxyRequest(string cluster, int statusCode, double durationSeconds);
    void RecordKafkaMessage(string topic, string status);
    void RecordKafkaConsumerLag(string topic, long lag);
}
