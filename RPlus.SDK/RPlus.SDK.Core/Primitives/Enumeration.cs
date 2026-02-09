// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Primitives.Enumeration
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable
namespace RPlus.SDK.Core.Primitives;

public abstract class Enumeration : IComparable
{
  public string Name { get; private set; }

  public int Id { get; private set; }

  protected Enumeration(int id, string name)
  {
    if (string.IsNullOrEmpty(name))
      throw new ArgumentException("Name must be provided", nameof(name));

    this.Id = id;
    this.Name = name;
  }

  public override string ToString() => this.Name;

  public static IEnumerable<T> GetAll<T>() where T : Enumeration
  {
    return typeof(T)
      .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
      .Select(f => f.GetValue(null))
      .OfType<T>();
  }

  public override bool Equals(object? obj)
  {
    return obj is Enumeration enumeration && this.GetType().Equals(obj.GetType()) && this.Id.Equals(enumeration.Id);
  }

  public override int GetHashCode() => this.Id.GetHashCode();

  public int CompareTo(object? other)
  {
    if (other is not Enumeration enumeration)
    {
      return 1;
    }

    return this.Id.CompareTo(enumeration.Id);
  }
}
