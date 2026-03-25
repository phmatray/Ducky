// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action finishes processing.
/// </summary>
public class ActionCompletedEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The context associated with the action.</param>
    public ActionCompletedEventArgs(ActionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// The context associated with the action.
    /// </summary>
    public ActionContext Context { get; }
}
