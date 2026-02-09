// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.OrgNode
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class OrgNode
{
  public Guid Id { get; set; }

  public Guid TenantId { get; set; }

  public Guid? ParentId { get; set; }

  public string Name { get; set; } = string.Empty;

  public string Type { get; set; } = string.Empty;

  public string Path { get; set; } = string.Empty;

  public JsonDocument? Attributes { get; set; }

  public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

  public DateTime? ValidTo { get; set; }

  public bool IsDeleted { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

  public OrgNode? Parent { get; set; }

  public ICollection<OrgNode> Children { get; set; } = (ICollection<OrgNode>) new List<OrgNode>();

  public ICollection<Position> Positions { get; set; } = (ICollection<Position>) new List<Position>();

  public ICollection<NodeContext> Contexts { get; set; } = (ICollection<NodeContext>) new List<NodeContext>();
}
