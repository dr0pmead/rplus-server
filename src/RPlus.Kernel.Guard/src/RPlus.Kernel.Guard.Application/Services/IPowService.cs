// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Application.Services.IPowService
// Assembly: RPlus.Kernel.Guard.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 82568AEC-3F33-4FE6-A0C6-A1DA0DDC1E1F
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\RPlus.Kernel.Guard.Application.dll

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Guard.Application.Services;

public interface IPowService
{
  Task<PowChallenge> CreateChallengeAsync(string? scope, CancellationToken ct);

  Task<PowVerifyResult> VerifyAsync(string challengeId, string nonce, CancellationToken ct);
}
