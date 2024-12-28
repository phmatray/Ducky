// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

/// <summary>
/// Represents the severity level of a notification.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Indicates a normal severity level for the notification.
    /// </summary>
    Normal,

    /// <summary>
    /// Indicates an informational severity level for the notification.
    /// </summary>
    Info,

    /// <summary>
    /// Indicates a success severity level for the notification.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates a warning severity level for the notification.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates an error severity level for the notification.
    /// </summary>
    Error
}
