// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.SodPolicySet
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class SodPolicySet
{
  public Guid Id { get; set; }

  public Guid? TenantId { get; set; }

  public int Version { get; set; }

  public SodPolicyStatus Status { get; set; }

  public Guid CreatedBy { get; set; }

  public DateTime CreatedAt { get; set; }

  public Guid? ApprovedBy { get; set; }

  public DateTime? ApprovedAt { get; set; }

  public List<SodPolicy> Policies { get; set; } = new List<SodPolicy>();

  [Timestamp]
  public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
