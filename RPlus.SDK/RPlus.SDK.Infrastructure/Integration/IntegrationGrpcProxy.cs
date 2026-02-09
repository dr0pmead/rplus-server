using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace RPlus.SDK.Infrastructure.Integration;

public sealed class IntegrationGrpcProxy : IIntegrationGrpcProxy
{
    private static readonly Marshaller<Struct> StructMarshaller = new(
        value => value.ToByteArray(),
        data => Struct.Parser.ParseFrom(data));

    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private readonly IntegrationGrpcProxyOptions _options;

    public IntegrationGrpcProxy(IOptions<IntegrationGrpcProxyOptions> options)
    {
        _options = options.Value;
    }

    public async Task<Struct> CallUnaryAsync(
        string service,
        string method,
        Struct request,
        CancellationToken cancellationToken)
    {
        if (!_options.Services.TryGetValue(service, out var address))
        {
            throw new InvalidOperationException("grpc_service_not_configured");
        }

        var channel = _channels.GetOrAdd(service, _ => GrpcChannel.ForAddress(address));
        var invoker = channel.CreateCallInvoker();
        var descriptor = new Method<Struct, Struct>(MethodType.Unary, service, method, StructMarshaller, StructMarshaller);
        var call = invoker.AsyncUnaryCall(descriptor, null, new CallOptions(cancellationToken: cancellationToken), request);
        return await call.ResponseAsync.ConfigureAwait(false);
    }
}
