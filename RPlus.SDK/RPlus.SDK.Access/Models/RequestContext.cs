using System;

#nullable enable
namespace RPlus.SDK.Access.Models;

public record RequestContext
{
  public string IpAddress { get; init; } = string.Empty;

  public string UserAgent { get; init; } = string.Empty;

  public DateTime Timestamp { get; init; }

  public string Channel { get; init; } = string.Empty;

  public RequestContext()
  {
  }
}
