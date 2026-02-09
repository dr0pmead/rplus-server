// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Attributes.PermissionAttribute
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;

#nullable enable
namespace RPlus.SDK.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute : Attribute
{
  public string PermissionId { get; }

  public string[] SupportedContexts { get; }

  public PermissionAttribute(string permissionId, params string[] contexts)
  {
    this.PermissionId = permissionId;
    this.SupportedContexts = contexts ?? Array.Empty<string>();
  }
}
