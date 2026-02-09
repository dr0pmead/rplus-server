// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.Position
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class Position
{
  public Guid Id { get; set; }

  public Guid TenantId { get; set; }

  public Guid NodeId { get; set; }

  public string Title { get; set; } = string.Empty;

  public int Level { get; set; }

  public Guid? ReportsToPositionId { get; set; }

  public bool IsVacant { get; set; } = true;

  public JsonDocument? Attributes { get; set; }

  public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

  public DateTime? ValidTo { get; set; }

  public bool IsDeleted { get; set; }

  public OrgNode? Node { get; set; }

  public Position? ReportsToPosition { get; set; }

  public ICollection<Position> DirectReports { get; set; } = (ICollection<Position>) new List<Position>();

  public ICollection<UserAssignment> Assignments { get; set; } = (ICollection<UserAssignment>) new List<UserAssignment>();

  public ICollection<PositionContext> Contexts { get; set; } = (ICollection<PositionContext>) new List<PositionContext>();
}
