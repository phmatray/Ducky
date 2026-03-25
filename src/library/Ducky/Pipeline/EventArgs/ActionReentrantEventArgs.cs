// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action is queued due to re-entrant dispatch.
/// </summary>
public class ActionReentrantEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionReentrantEventArgs"/> class.
    /// </summary>
    /// <param name="action">The action that was queued for deferred processing.</param>
    /// <param name="currentAction">The action that was being processed when re-entrancy was detected.</param>
    /// <param name="queueDepth">The number of actions in the re-entrant queue at the time of queuing.</param>
    public ActionReentrantEventArgs(object action, object currentAction, int queueDepth)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        CurrentAction = currentAction ?? throw new ArgumentNullException(nameof(currentAction));
        QueueDepth = queueDepth;
    }

    /// <summary>
    /// The action that was queued for deferred processing.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// The action that was being processed when re-entrancy was detected.
    /// </summary>
    public object CurrentAction { get; }

    /// <summary>
    /// The number of actions in the re-entrant queue at the time of queuing.
    /// </summary>
    public int QueueDepth { get; }
}
