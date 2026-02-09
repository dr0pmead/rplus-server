using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RPlus.SDK.Eventing.SchemaRegistry;

public interface IEventSchemaPublisher
{
    Task PublishAllAsync(CancellationToken ct = default);
}

public interface IEventSchemaSource
{
    IReadOnlyList<EventSchemaDescriptor> GetSchemas();
}

