namespace RPlus.SDK.Core.Abstractions;

public interface IRPlusMetrics
{
  void IncrementCounter(string name, params (string Key, string Value)[] tags);

  void RecordGauge(string name, double value, params (string Key, string Value)[] tags);

  void RecordHistogram(string name, double value, params (string Key, string Value)[] tags);
}
