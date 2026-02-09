// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.IReadProjection`1
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public interface IReadProjection<in TEvent>
{
  Task Apply(TEvent evt);
}
