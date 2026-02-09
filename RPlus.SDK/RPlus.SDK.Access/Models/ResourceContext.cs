using System;

#nullable enable
namespace RPlus.SDK.Access.Models;

public record ResourceContext
{
  public Guid? NodeId { get; init; }

  public string? ResourceId { get; init; }

  public string? ResourceType { get; init; }

  public Guid? OwnerId { get; init; }

  public ResourceContext()
  {
  }
}
