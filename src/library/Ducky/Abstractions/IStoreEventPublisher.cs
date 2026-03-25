// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky;

/// <summary>
/// Publishes store events and allows subscribers to listen for them.
/// </summary>
public interface IStoreEventPublisher
{
    /// <summary>
    /// Occurs when a store event is published.
    /// </summary>
    event EventHandler<StoreEventArgs>? EventPublished;

    /// <summary>
    /// Publishes a new pipeline event to all subscribers.
    /// </summary>
    /// <param name="storeEvent">The event instance to publish.</param>
    void Publish(StoreEventArgs storeEvent);
}
