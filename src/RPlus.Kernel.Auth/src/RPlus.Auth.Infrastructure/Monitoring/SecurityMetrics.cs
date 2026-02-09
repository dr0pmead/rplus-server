// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Infrastructure.Monitoring.SecurityMetrics
// Assembly: RPlus.Auth.Infrastructure, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C6806E10-ACC6-4CD0-B785-E31754B39FE4
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Infrastructure.dll

using Prometheus;
using RPlus.Auth.Application.Interfaces;
using System.Diagnostics.Metrics;

#nullable enable
namespace RPlus.Auth.Infrastructure.Monitoring;

public class SecurityMetrics : ISecurityMetrics
{
  private static readonly Counter OtpRequestsTotal;
  private static readonly Counter OtpVerificationsTotal;
  private static readonly Counter TokensIssuedTotal;
  private static readonly Counter TokenRefreshesTotal;
  private static readonly Counter SecurityRiskDetectedTotal;
  private static readonly Counter SessionsRevokedTotal;
  private static readonly Counter LoginAttemptsTotal;

  public SecurityMetrics(IMeterFactory meterFactory)
  {
  }

  public void IncOtpRequest(string type = "sms", string status = "initiated")
  {
    SecurityMetrics.OtpRequestsTotal.WithLabels(new string[2]
    {
      type,
      status
    }).Inc(1.0);
  }

  public void IncOtpVerification(string status)
  {
    SecurityMetrics.OtpVerificationsTotal.WithLabels(new string[1]
    {
      status
    }).Inc(1.0);
  }

  public void IncTokenIssued() => SecurityMetrics.TokensIssuedTotal.Inc(1.0);

  public void IncTokenRefresh(string status)
  {
    SecurityMetrics.TokenRefreshesTotal.WithLabels(new string[1]
    {
      status
    }).Inc(1.0);
  }

  public void IncRiskDetected(string level)
  {
    SecurityMetrics.SecurityRiskDetectedTotal.WithLabels(new string[1]
    {
      level
    }).Inc(1.0);
  }

  public void IncSessionRevoked(string reason)
  {
    SecurityMetrics.SessionsRevokedTotal.WithLabels(new string[1]
    {
      reason
    }).Inc(1.0);
  }

  public void IncLoginAttempt(string status, string? failureReason = null)
  {
    SecurityMetrics.LoginAttemptsTotal.WithLabels(new string[2]
    {
      status,
      failureReason ?? "none"
    }).Inc(1.0);
  }

  static SecurityMetrics()
  {
    CounterConfiguration configuration1 = new CounterConfiguration();
    configuration1.LabelNames = new string[2]
    {
      "type",
      "status"
    };
    SecurityMetrics.OtpRequestsTotal = Prometheus.Metrics.CreateCounter("auth_otp_requests_total", "Total number of OTP requests", configuration1);
    CounterConfiguration configuration2 = new CounterConfiguration();
    configuration2.LabelNames = new string[1]{ "status" };
    SecurityMetrics.OtpVerificationsTotal = Prometheus.Metrics.CreateCounter("auth_otp_verifications_total", "Results of OTP verifications", configuration2);
    SecurityMetrics.TokensIssuedTotal = Prometheus.Metrics.CreateCounter("auth_token_issued_total", "Total number of tokens issued", (CounterConfiguration) null);
    CounterConfiguration configuration3 = new CounterConfiguration();
    configuration3.LabelNames = new string[1]{ "status" };
    SecurityMetrics.TokenRefreshesTotal = Prometheus.Metrics.CreateCounter("auth_token_refreshed_total", "Results of token refreshes", configuration3);
    CounterConfiguration configuration4 = new CounterConfiguration();
    configuration4.LabelNames = new string[1]{ "level" };
    SecurityMetrics.SecurityRiskDetectedTotal = Prometheus.Metrics.CreateCounter("auth_security_risk_detected_total", "Security risk detection events", configuration4);
    CounterConfiguration configuration5 = new CounterConfiguration();
    configuration5.LabelNames = new string[1]{ "reason" };
    SecurityMetrics.SessionsRevokedTotal = Prometheus.Metrics.CreateCounter("auth_sessions_revoked_total", "Total number of sessions revoked", configuration5);
    CounterConfiguration configuration6 = new CounterConfiguration();
    configuration6.LabelNames = new string[2]
    {
      "status",
      "reason"
    };
    SecurityMetrics.LoginAttemptsTotal = Prometheus.Metrics.CreateCounter("auth_login_attempts_total", "Total login attempts", configuration6);
  }
}
