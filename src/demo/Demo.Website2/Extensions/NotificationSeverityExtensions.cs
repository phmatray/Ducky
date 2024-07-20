// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.Website2.Extensions;

/// <summary>
/// Extension methods for converting <see cref="NotificationSeverity"/> to MudBlazor components.
/// </summary>
public static class NotificationSeverityExtensions
{
    /// <summary>
    /// Converts a <see cref="NotificationSeverity"/> to a MudBlazor <see cref="Severity"/>.
    /// </summary>
    /// <param name="severity">The <see cref="NotificationSeverity"/> to convert.</param>
    /// <returns>The corresponding MudBlazor <see cref="Severity"/>.</returns>
    public static Severity ToMudSeverity(this NotificationSeverity severity)
    {
        return (Severity)(int)severity;
    }

    /// <summary>
    /// Converts a <see cref="NotificationSeverity"/> to a MudBlazor <see cref="Color"/>.
    /// </summary>
    /// <param name="severity">The <see cref="NotificationSeverity"/> to convert.</param>
    /// <returns>The corresponding MudBlazor <see cref="Color"/>.</returns>
    public static Color ToMudColor(this NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Info => Color.Info,
            NotificationSeverity.Success => Color.Success,
            NotificationSeverity.Warning => Color.Warning,
            NotificationSeverity.Error => Color.Error,
            _ => Color.Primary
        };
    }
}
