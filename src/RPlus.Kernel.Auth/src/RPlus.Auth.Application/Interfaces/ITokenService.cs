// Decompiled with JetBrains decompiler
// Type: RPlus.Auth.Application.Interfaces.ITokenService
// Assembly: RPlus.Auth.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 94419EED-98B7-4B52-A7B5-E1ADD668651C
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-auth\RPlus.Auth.Application.dll

using RPlus.Auth.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Auth.Application.Interfaces;

public interface ITokenService
{
  Task<TokenPair> IssueTokensAsync(
    AuthUserEntity user,
    DeviceEntity device,
    string? dpopThumbprint,
    string? ip,
    string? userAgent,
    CancellationToken cancellationToken);

  Task<TokenOperationResult> RefreshAsync(
    string refreshToken,
    string deviceIdentifier,
    string? dpopThumbprint,
    string? ip,
    string? userAgent,
    CancellationToken cancellationToken);

  Task RevokeAsync(
    string refreshToken,
    string deviceIdentifier,
    CancellationToken cancellationToken);
}
