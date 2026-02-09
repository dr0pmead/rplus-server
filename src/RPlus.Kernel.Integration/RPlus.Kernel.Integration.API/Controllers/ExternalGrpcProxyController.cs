// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Api.Controllers.ExternalGrpcProxyController
// Assembly: ExecuteService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C69F7836-BD02-4299-8BB3-623377DB3595
// Assembly location: F:\RPlus Framework\Recovery\integration\app\ExecuteService.dll

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Mvc;
using RPlus.SDK.Contracts.External;
using RPlus.SDK.Infrastructure.Integration;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Api.Controllers;

[ApiController]
[Route("external/v1/proxy")]
public class ExternalGrpcProxyController : ControllerBase
{
  private readonly IIntegrationGrpcProxy _proxy;

  public ExternalGrpcProxyController(IIntegrationGrpcProxy proxy) => this._proxy = proxy;

  [HttpPost("{service}/{method}")]
  [RPlus.SDK.Core.Abstractions.External("external.proxy.invoke", null)]
  public async Task<IActionResult> Call(
    string service,
    string method,
    [FromBody] JsonElement payload,
    CancellationToken cancellationToken)
  {
    ExternalGrpcProxyController grpcProxyController = this;
    string correlationId = grpcProxyController.HttpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault<string>() ?? grpcProxyController.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault<string>() ?? Guid.NewGuid().ToString();
    Struct request = JsonParser.Default.Parse<Struct>(payload.GetRawText());
    IActionResult actionResult;
    using (JsonDocument jsonDocument = JsonDocument.Parse(JsonFormatter.Default.Format((IMessage) await grpcProxyController._proxy.CallUnaryAsync(service, method, request, cancellationToken))))
    {
      ExternalResult<JsonElement> externalResult = ExternalResult<JsonElement>.Ok(jsonDocument.RootElement.Clone(), correlationId);
      actionResult = (IActionResult) grpcProxyController.Ok((object) externalResult);
    }
    correlationId = (string) null;
    return actionResult;
  }
}
