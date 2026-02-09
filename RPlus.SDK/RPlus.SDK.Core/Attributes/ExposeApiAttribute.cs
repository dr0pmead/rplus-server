// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Attributes.ExposeApiAttribute
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ExposeApiAttribute : Attribute
{
  public string Route { get; }

  public string Method { get; }

  public bool IsPublic { get; set; }

  public bool Idempotent { get; set; }

  public ExposeApiAttribute(string route, string method = "POST")
  {
    this.Route = route;
    this.Method = method.ToUpperInvariant();
  }
}
