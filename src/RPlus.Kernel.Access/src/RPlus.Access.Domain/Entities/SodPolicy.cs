// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.SodPolicy
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class SodPolicy
{
  public Guid Id { get; set; }

  public Guid PolicySetId { get; set; }

  public List<string> ConflictRoles { get; set; } = new List<string>();

  public string Scope { get; set; } = "GLOBAL";

  public SodSeverity Severity { get; set; }

  public string Description { get; set; } = string.Empty;

  public SodPolicySet? PolicySet { get; set; }
}
