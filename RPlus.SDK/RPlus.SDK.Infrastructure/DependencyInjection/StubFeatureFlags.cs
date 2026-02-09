// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.DependencyInjection.StubFeatureFlags
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using RPlus.SDK.Core.Abstractions;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Infrastructure.DependencyInjection;

public class StubFeatureFlags : IFeatureFlags
{
  public Task<bool> IsEnabledAsync(string featureName) => Task.FromResult<bool>(true);

  public Task<bool> IsEnabledAsync<TContext>(string featureName, TContext context)
  {
    return Task.FromResult<bool>(true);
  }
}
