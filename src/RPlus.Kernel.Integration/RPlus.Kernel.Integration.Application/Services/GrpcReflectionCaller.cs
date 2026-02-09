// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.GrpcReflectionCaller
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public class GrpcReflectionCaller : IGrpcReflectionCaller
{
  private readonly ILogger<GrpcReflectionCaller> _logger;
  private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new ConcurrentDictionary<string, GrpcChannel>();

  public GrpcReflectionCaller(ILogger<GrpcReflectionCaller> logger) => this._logger = logger;

  public async Task<GrpcReflectionCaller.ProxyResult> CallDynamicAsync(
    string targetHost,
    string serviceName,
    string methodName,
    byte[] payload,
    Metadata metadata,
    CancellationToken cancellationToken)
  {
    if (!targetHost.StartsWith("http"))
      targetHost = "http://" + targetHost;
    GrpcChannel orAdd = this._channels.GetOrAdd(targetHost, (Func<string, GrpcChannel>) (h => GrpcChannel.ForAddress(h)));
    string str = $"{serviceName}/{methodName}";
    Method<byte[], byte[]> method = new Method<byte[], byte[]>(MethodType.Unary, serviceName, methodName, Marshallers.Create<byte[]>((Func<byte[], byte[]>) (b => b), (Func<byte[], byte[]>) (b => b)), Marshallers.Create<byte[]>((Func<byte[], byte[]>) (b => b), (Func<byte[], byte[]>) (b => b)));
    CallInvoker callInvoker = orAdd.CreateCallInvoker();
    try
    {
      Metadata headers = metadata;
      CancellationToken cancellationToken1 = cancellationToken;
      DateTime? deadline = new DateTime?();
      CancellationToken cancellationToken2 = cancellationToken1;
      CallOptions options = new CallOptions(headers, deadline, cancellationToken2);
      byte[] numArray = await callInvoker.AsyncUnaryCall<byte[], byte[]>(method, targetHost, options, payload);
      return new GrpcReflectionCaller.ProxyResult()
      {
        Success = true,
        Response = numArray,
        StatusCode = 200
      };
    }
    catch (RpcException ex)
    {
      this._logger.LogWarning((Exception) ex, "gRPC Proxy Error: {Status}", (object) ex.Status);
      return new GrpcReflectionCaller.ProxyResult()
      {
        Success = false,
        Error = ex.Status.Detail,
        StatusCode = (int) ex.StatusCode
      };
    }
    catch (Exception ex)
    {
      this._logger.LogError(ex, "Unexpected Proxy Error");
      return new GrpcReflectionCaller.ProxyResult()
      {
        Success = false,
        Error = "internal_proxy_error",
        StatusCode = 500
      };
    }
  }

  public class ProxyResult
  {
    public bool Success { get; set; }

    public byte[]? Response { get; set; }

    public string? Error { get; set; }

    public int StatusCode { get; set; }
  }
}
