// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.ExternalAttribute
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ExternalAttribute : Attribute
{
  public ExternalAttribute(string? scope = null, string? context = null)
  {
    this.Scope = scope;
    this.Context = context;
  }

  public string? Scope { get; }

  public string? Context { get; }
}
