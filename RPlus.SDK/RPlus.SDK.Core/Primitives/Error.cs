// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Primitives.Error
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System.Collections.Generic;

#nullable enable
namespace RPlus.SDK.Core.Primitives;

public sealed record Error
{
  public static readonly Error None = new Error(string.Empty, string.Empty, ErrorType.Failure);
  public static readonly Error NullValue = new Error("Error.NullValue", "The specified result value is null.", ErrorType.Failure);

  public string Code { get; }

  public string Description { get; }

  public ErrorType Type { get; }

  public IDictionary<string, object?> Metadata { get; }

  public Error(
    string code,
    string description,
    ErrorType type,
    IDictionary<string, object?>? metadata = null)
  {
    this.Code = code;
    this.Description = description;
    this.Type = type;
    this.Metadata = metadata ?? (IDictionary<string, object?>) new Dictionary<string, object?>();
  }

  public static Error Failure(string code, string description)
  {
    return new Error(code, description, ErrorType.Failure);
  }

  public static Error Validation(string code, string description)
  {
    return new Error(code, description, ErrorType.Validation);
  }

  public static Error NotFound(string code, string description)
  {
    return new Error(code, description, ErrorType.NotFound);
  }

  public static Error Conflict(string code, string description)
  {
    return new Error(code, description, ErrorType.Conflict);
  }

  public static Error Unauthorized(string code, string description)
  {
    return new Error(code, description, ErrorType.Authentication);
  }

  public static Error Forbidden(string code, string description)
  {
    return new Error(code, description, ErrorType.Authorization);
  }
}
