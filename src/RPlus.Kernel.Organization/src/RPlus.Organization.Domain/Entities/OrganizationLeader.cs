// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.OrganizationLeader
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using RPlus.Organization.Domain.Enums;
using System;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class OrganizationLeader
{
  public Guid Id { get; set; }

  public Guid OrganizationId { get; set; }

  public Guid UserId { get; set; }

  public LeaderRole Role { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public RPlus.Organization.Domain.Entities.Organization? Organization { get; set; }
}
