using Microsoft.Extensions.Options;
using RPlus.SDK.Eventing.SchemaRegistry;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.SDK.Infrastructure.SchemaRegistry;

public interface IEventSchemaRegistryReader
{
    Task<IReadOnlyList<EventSchemaDescriptor>> GetAllAsync(CancellationToken ct = default);
}

public sealed class EventSchemaRegistryReader : IEventSchemaRegistryReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<EventSchemaRegistryOptions> _options;

    public EventSchemaRegistryReader(IConnectionMultiplexer redis, IOptionsMonitor<EventSchemaRegistryOptions> options)
    {
        _redis = redis;
        _options = options;
    }

    public async Task<IReadOnlyList<EventSchemaDescriptor>> GetAllAsync(CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        var db = _redis.GetDatabase();

        var entries = await db.HashGetAllAsync(opts.RedisHashKey).ConfigureAwait(false);
        if (entries.Length == 0)
        {
            return Array.Empty<EventSchemaDescriptor>();
        }

        var list = new List<EventSchemaDescriptor>(entries.Length);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.Value.IsNullOrEmpty)
            {
                continue;
            }

            try
            {
                var schema = JsonSerializer.Deserialize<EventSchemaDescriptor>(entry.Value.ToString(), SerializerOptions);
                if (schema != null)
                {
                    list.Add(schema);
                }
            }
            catch
            {
                // ignore invalid entries
            }
        }

        return list
            .OrderBy(s => s.EventType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
