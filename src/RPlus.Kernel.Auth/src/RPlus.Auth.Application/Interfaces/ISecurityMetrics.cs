// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.ISecurityMetrics
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface ISecurityMetrics
{
  void IncTokenRefresh(string status);

  void IncTokenIssued();

  void IncRiskDetected(string level);

  void IncSessionRevoked(string reason);

  void IncLoginAttempt(string status, string? failureReason = null);

  void IncOtpRequest(string type = "sms", string status = "initiated");

  void IncOtpVerification(string status);
}
