using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Models;

public record AccessContext
{
  public IdentityContext Identity { get; init; } = new();

  public AuthenticationContext Authentication { get; init; } = new();

  public ResourceContext Resource { get; init; } = new();

  public RequestContext Request { get; init; } = new();

  public Dictionary<string, object> Attributes { get; init; } = new();

  public AccessContext()
  {
  }
}
