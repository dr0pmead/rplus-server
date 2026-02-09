// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.UserAssignment
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class UserAssignment
{
  public Guid Id { get; set; }

  public Guid TenantId { get; set; }

  public Guid UserId { get; set; }

  public Guid PositionId { get; set; }

  public Guid NodeId { get; set; }

  // HEAD / DEPUTY / EMPLOYEE
  public string Role { get; set; } = "EMPLOYEE";

  public string Type { get; set; } = "REGULAR";

  public Guid? ReplacementForAssignmentId { get; set; }

  public bool IsPrimary { get; set; }

  public Decimal FtePercent { get; set; } = 100.00M;

  public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

  public DateTime? ValidTo { get; set; }

  public bool IsDeleted { get; set; }

  public Position? Position { get; set; }

  public OrgNode? Node { get; set; }

  public UserAssignment? ReplacementForAssignment { get; set; }

  public ICollection<UserRoleOverride> RoleOverrides { get; set; } = (ICollection<UserRoleOverride>) new List<UserRoleOverride>();
}
