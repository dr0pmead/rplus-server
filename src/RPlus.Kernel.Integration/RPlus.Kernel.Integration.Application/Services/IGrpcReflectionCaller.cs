// Decompiled with JetBrains decompiler
// Type: RPlus.Kernel.Integration.Application.Services.IGrpcReflectionCaller
// Assembly: RPlus.Kernel.Integration.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: C48B19BB-641F-4A32-A8FE-89CEE109A05C
// Assembly location: F:\RPlus Framework\Recovery\integration\app\RPlus.Kernel.Integration.Application.dll

using Grpc.Core;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.Kernel.Integration.Application.Services;

public interface IGrpcReflectionCaller
{
  Task<GrpcReflectionCaller.ProxyResult> CallDynamicAsync(
    string targetHost,
    string serviceName,
    string methodName,
    byte[] payload,
    Metadata metadata,
    CancellationToken cancellationToken);
}
