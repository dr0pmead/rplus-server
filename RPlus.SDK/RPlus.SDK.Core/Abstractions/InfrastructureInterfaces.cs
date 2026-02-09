// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Core.Abstractions.IRPlusCache
// Assembly: RPlus.SDK.Core, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: C7BF4574-BF4E-421C-9B89-0A828A452EA1
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Core.dll

using System;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Core.Abstractions;

public interface IRPlusCache
{
  Task<T?> GetAsync<T>(string key);

  Task SetAsync<T>(string key, T value, TimeSpan? ttl = null);

  Task RemoveAsync(string key);
}
