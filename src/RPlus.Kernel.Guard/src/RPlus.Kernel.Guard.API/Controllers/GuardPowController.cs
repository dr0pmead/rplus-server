// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Guard.Api.Controllers.GuardPowController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 6C1F5346-815B-4C0D-BD63-391C84B5BE3F
// Assembly location: F:\RPlus Framework\Recovery\rplus-kernel-guard\ExecuteService.dll

using Microsoft.AspNetCore.Mvc;
using RPlus.Kernel.Guard.Application.Services;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Guard.Api.Controllers;

[ApiController]
[Route("pow")]
public sealed class GuardPowController : ControllerBase
{
  private readonly IPowService _powService;

  public GuardPowController(IPowService powService) => this._powService = powService;

  [HttpPost("challenge")]
  public async Task<IActionResult> CreateChallenge(
    [FromBody] PowChallengeRequest? request,
    CancellationToken ct)
  {
    GuardPowController guardPowController = this;
    PowChallenge challengeAsync = await guardPowController._powService.CreateChallengeAsync(request?.Scope, ct);
    return (IActionResult) guardPowController.Ok((object) new PowChallengeResponse(challengeAsync.ChallengeId, challengeAsync.Salt, challengeAsync.Difficulty, challengeAsync.ExpiresAt, challengeAsync.Scope));
  }

  [HttpPost("verify")]
  public async Task<IActionResult> Verify([FromBody] PowVerifyRequest request, CancellationToken ct)
  {
    GuardPowController guardPowController = this;
    PowVerifyResult powVerifyResult = await guardPowController._powService.VerifyAsync(request.ChallengeId, request.Nonce, ct);
    return powVerifyResult.IsValid ? (IActionResult) guardPowController.Ok((object) new PowVerifyResponse(true, (string) null, powVerifyResult.Hash)) : (IActionResult) guardPowController.Ok((object) new PowVerifyResponse(false, powVerifyResult.Error ?? "pow_failed", powVerifyResult.Hash));
  }
}
