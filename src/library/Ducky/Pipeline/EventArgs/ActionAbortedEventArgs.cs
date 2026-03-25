// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action is aborted during processing.
/// </summary>
public class ActionAbortedEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionAbortedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The context associated with the action.</param>
    /// <param name="reason">The reason for aborting the action.</param>
    public ActionAbortedEventArgs(ActionContext context, string reason)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// The context associated with the action.
    /// </summary>
    public ActionContext Context { get; }

    /// <summary>
    /// The reason for aborting the action.
    /// </summary>
    public string Reason { get; }
}
