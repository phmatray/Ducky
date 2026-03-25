// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky.Middlewares.CorrelationId;

/// <summary>
/// Event published when a correlation ID is assigned to an action.
/// </summary>
public class CorrelationIdAssignedEvent : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdAssignedEvent"/> class.
    /// </summary>
    /// <param name="action">The action object.</param>
    /// <param name="correlationId">The assigned correlation ID.</param>
    public CorrelationIdAssignedEvent(object action, in Guid correlationId)
    {
        Action = action;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// The action object.
    /// </summary>
    public object Action { get; init; }

    /// <summary>
    /// The assigned correlation ID.
    /// </summary>
    public Guid CorrelationId { get; init; }
}
