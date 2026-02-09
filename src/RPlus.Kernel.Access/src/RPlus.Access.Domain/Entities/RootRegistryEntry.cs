// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.RootRegistryEntry
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable enable
namespace RPlus.Access.Domain.Entities;

[Table("root_registry", Schema = "access")]
public class RootRegistryEntry
{
  [Key]
  public string HashedUserId { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }

  public string Status { get; set; } = "ACTIVE";
}
