using System;
using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Access.Models;

public record AuthenticationContext
{
  public int Aal { get; init; }

  public List<string> Amr { get; init; } = new();

  public DateTime? AuthTime { get; init; }

  public string? DeviceId { get; init; }

  public AuthenticationContext()
  {
  }
}
