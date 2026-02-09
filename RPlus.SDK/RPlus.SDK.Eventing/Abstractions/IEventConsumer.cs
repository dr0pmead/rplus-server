// Decompiled with JetBrains decompiler
// Type: RPlus.SDK.Eventing.Abstractions.IEventConsumer`1
// Assembly: RPlus.SDK.Eventing, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 33A42332-9F9D-4559-BE1F-4385252A1184
// Assembly location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Eventing.dll
// XML documentation location: F:\RPlus Framework\Recovery\loyalty\RPlus.SDK.Eventing.xml

using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace RPlus.SDK.Eventing.Abstractions;

/// <summary>
/// Defines the contract for consuming domain events.
/// This interface should be implemented by any service handler that reacts to domain events.
/// </summary>
/// <typeparam name="T">The type of the event payload to handle.</typeparam>
public interface IEventConsumer<T> where T : class
{
  /// <summary>Handles the incoming domain event.</summary>
  /// <param name="envelope">The full event envelope including metadata and tracing info.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task ConsumeAsync(EventEnvelope<T> envelope, CancellationToken cancellationToken);
}
