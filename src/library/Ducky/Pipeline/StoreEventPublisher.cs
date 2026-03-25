// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Default implementation of <see cref="IStoreEventPublisher"/>.
/// </summary>
public sealed class StoreEventPublisher : IStoreEventPublisher, IDisposable
{
    /// <inheritdoc />
    public event EventHandler<StoreEventArgs>? EventPublished;

    /// <inheritdoc />
    public void Publish(StoreEventArgs storeEvent)
    {
        EventPublished?.Invoke(this, storeEvent);
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public void Dispose()
    {
        EventPublished = null;
    }
}
