// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.ServiceRegistryEntry
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class ServiceRegistryEntry
{
  public Guid Id { get; set; }

  [Required]
  [MaxLength(100)]
  public string ServiceName { get; set; } = string.Empty;

  [Required]
  [MaxLength(255 /*0xFF*/)]
  public string BaseUrl { get; set; } = string.Empty;

  public string PublicKeys { get; set; } = "{}";

  public ServiceCriticality Criticality { get; set; }

  public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
