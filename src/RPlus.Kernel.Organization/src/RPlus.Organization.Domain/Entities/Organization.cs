// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.Organization
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class Organization
{
  public Guid Id { get; set; }

  public Guid? ParentId { get; set; }

  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  public JsonDocument? Metadata { get; set; }

  public JsonDocument? Rules { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

  public RPlus.Organization.Domain.Entities.Organization? Parent { get; set; }

  public ICollection<RPlus.Organization.Domain.Entities.Organization> Children { get; set; } = (ICollection<RPlus.Organization.Domain.Entities.Organization>) new List<RPlus.Organization.Domain.Entities.Organization>();

  public ICollection<OrganizationLeader> Leaders { get; set; } = (ICollection<OrganizationLeader>) new List<OrganizationLeader>();

  public ICollection<OrganizationMember> Members { get; set; } = (ICollection<OrganizationMember>) new List<OrganizationMember>();
}
