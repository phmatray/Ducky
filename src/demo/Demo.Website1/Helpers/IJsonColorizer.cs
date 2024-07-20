// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.Website1.Helpers;

/// <summary>
/// Defines a method to colorize a JSON string to an HTML string with color coding.
/// </summary>
public interface IJsonColorizer
{
    /// <summary>
    /// Colorizes a JSON string to an HTML string with color coding.
    /// </summary>
    /// <param name="json">The JSON string to colorize.</param>
    /// <returns>An HTML string with color-coded JSON.</returns>
    string ColorizeJson(string json);
}
