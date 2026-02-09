// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Audit.Domain.Entities.AuditEvent
// Assembly: RPlus.Kernel.Audit.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 005C004C-7DDA-4A11-A8F2-5AF64ACE33B4
// Assembly location: F:\RPlus Framework\Recovery\audit\RPlus.Kernel.Audit.Domain.dll

using RPlus.Kernel.Audit.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable
namespace RPlus.Kernel.Audit.Domain.Entities;

public class AuditEvent
{
  public Guid Id { get; private set; }

  public EventSource Source { get; private set; }

  public AuditEventType EventType { get; private set; }

  public EventSeverity Severity { get; private set; }

  public string Actor { get; private set; }

  public string Action { get; private set; }

  public string Resource { get; private set; }

  public Dictionary<string, object> Metadata { get; private set; }

  public DateTime Timestamp { get; private set; }

  public string EventHash { get; private set; }

  public string PreviousEventHash { get; private set; }

  public string? Signature { get; private set; }

  public string? SignerId { get; private set; }

  public AuditEvent(
    EventSource source,
    AuditEventType eventType,
    EventSeverity severity,
    string actor,
    string action,
    string resource,
    Dictionary<string, object>? metadata = null,
    string? previousEventHash = null,
    string? signature = null,
    string? signerId = null)
  {
    this.Id = Guid.NewGuid();
    this.Source = source;
    this.EventType = eventType;
    this.Severity = severity;
    this.Actor = AuditEvent.MaskSensitiveData(actor);
    this.Action = action;
    this.Resource = resource;
    this.Metadata = AuditEvent.MaskMetadata(metadata ?? new Dictionary<string, object>());
    this.Timestamp = DateTime.UtcNow;
    this.PreviousEventHash = previousEventHash ?? string.Empty;
    this.Signature = signature;
    this.SignerId = signerId;
    this.EventHash = this.ComputeHash();
  }

  private string ComputeHash()
  {
    string s = $"{this.Id}|{this.Source}|{this.EventType}|{this.Actor}|{this.Action}|{this.Resource}|{this.Timestamp:O}|{this.PreviousEventHash}";
    using (SHA256 shA256 = SHA256.Create())
      return Convert.ToHexString(shA256.ComputeHash(Encoding.UTF8.GetBytes(s)));
  }

  public bool VerifyIntegrity() => this.EventHash == this.ComputeHash();

  private static string MaskSensitiveData(string input)
  {
    if (string.IsNullOrEmpty(input))
      return input;
    input = Regex.Replace(input, "\\+?\\d{10,15}", "***PHONE***");
    input = Regex.Replace(input, "[\\w\\.-]+@[\\w\\.-]+\\.\\w+", "***EMAIL***");
    return input;
  }

  private static Dictionary<string, object> MaskMetadata(Dictionary<string, object> metadata)
  {
    Dictionary<string, object> dictionary = new Dictionary<string, object>();
    foreach (KeyValuePair<string, object> keyValuePair in metadata)
      dictionary[keyValuePair.Key] = keyValuePair.Key.ToLower().Contains("password") || keyValuePair.Key.ToLower().Contains("secret") ? (object) "***REDACTED***" : (!(keyValuePair.Value is string input) ? keyValuePair.Value : (object) AuditEvent.MaskSensitiveData(input));
    return dictionary;
  }

  public static AuditEvent FromExternal(
    Guid id,
    EventSource source,
    AuditEventType eventType,
    EventSeverity severity,
    string actor,
    string action,
    string resource,
    Dictionary<string, object>? metadata,
    DateTime timestamp,
    string previousEventHash,
    string? signature,
    string? signerId)
  {
    AuditEvent auditEvent = new AuditEvent();
    auditEvent.Id = id;
    auditEvent.Source = source;
    auditEvent.EventType = eventType;
    auditEvent.Severity = severity;
    auditEvent.Actor = AuditEvent.MaskSensitiveData(actor);
    auditEvent.Action = action;
    auditEvent.Resource = resource;
    auditEvent.Metadata = AuditEvent.MaskMetadata(metadata ?? new Dictionary<string, object>());
    auditEvent.Timestamp = timestamp;
    auditEvent.PreviousEventHash = previousEventHash;
    auditEvent.Signature = signature;
    auditEvent.SignerId = signerId;
    auditEvent.EventHash = auditEvent.ComputeHash();
    return auditEvent;
  }

  private AuditEvent()
  {
    this.Actor = string.Empty;
    this.Action = string.Empty;
    this.Resource = string.Empty;
    this.Metadata = new Dictionary<string, object>();
    this.EventHash = string.Empty;
    this.PreviousEventHash = string.Empty;
  }
}
