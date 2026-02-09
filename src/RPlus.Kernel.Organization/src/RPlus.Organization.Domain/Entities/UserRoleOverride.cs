// Decompiled with JetBrains decompiler
// Type: RPlus.Organization.Domain.Entities.UserRoleOverride
// Assembly: RPlus.Organization.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 9B74ECA8-A357-445A-856F-E29D7584499E
// Assembly location: F:\RPlus Framework\Recovery\organization\RPlus.Organization.Domain.dll

using System;

#nullable enable
namespace RPlus.Organization.Domain.Entities;

public class UserRoleOverride
{
  public Guid AssignmentId { get; set; }

  public string RoleCode { get; set; } = string.Empty;

  public Guid CreatedBy { get; set; }

  public string? Reason { get; set; }

  public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

  public DateTime? ValidTo { get; set; }

  public UserAssignment? Assignment { get; set; }
}
