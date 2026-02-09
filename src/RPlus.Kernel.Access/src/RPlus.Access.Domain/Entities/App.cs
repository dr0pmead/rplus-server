// Decompiled with JetBrains decompiler
// Type: RPlus.Access.Domain.Entities.App
// Assembly: RPlus.Access.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 12800C08-0BE2-4CF5-B655-8F2F1D8374DF
// Assembly location: F:\RPlus Framework\Recovery\access\RPlus.Access.Domain.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Access.Domain.Entities;

public class App
{
  public Guid Id { get; set; }

  public string Code { get; set; } = string.Empty;

  public string? Name { get; set; }

  public ICollection<Permission> Permissions { get; set; } = (ICollection<Permission>) new List<Permission>();
}
