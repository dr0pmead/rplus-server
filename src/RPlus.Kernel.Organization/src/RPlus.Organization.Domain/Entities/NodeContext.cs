// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.NodeContext
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;
using System.Text.Json;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class NodeContext
{
  public Guid Id { get; set; }

  public Guid TenantId { get; set; }

  public Guid NodeId { get; set; }

  public string ResourceType { get; set; } = string.Empty;

  public JsonDocument Data { get; set; } = JsonDocument.Parse("{}");

  public string InheritanceStrategy { get; set; } = "MERGE";

  public long Version { get; set; } = 1;

  public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

  public DateTime? ValidTo { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

  public OrgNode? Node { get; set; }
}
