// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Infrastructure.Integration.IIntegRateLimiter
// Assembly: RPlus.SDK.Infrastructure, Version=1.0.53.0, Culture=neutral, PublicKeyToken=null
// MVID: 090B56FB-83A1-4463-9A61-BACE8A439AC5
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Infrastructure.dll

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Infrastructure.Integration;

public interface IIntegRateLimiter
{
  Task<bool> IsAllowedAsync(
    ApiKeyMetadata metadata,
    string scope,
    CancellationToken cancellationToken);

  Task<bool> IsWithinQuotaAsync(ApiKeyMetadata metadata, CancellationToken cancellationToken);
}
