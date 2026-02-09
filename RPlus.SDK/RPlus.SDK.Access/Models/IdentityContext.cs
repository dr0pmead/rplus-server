using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Models;

public record IdentityContext
{
  public Guid UserId { get; init; }

  public Guid TenantId { get; init; }

  public HashSet<string> EffectiveRoles { get; init; } = new();

  public Guid? ActingUserId { get; init; }

  public Guid? DelegationId { get; init; }

  public IdentityContext()
  {
  }
}
