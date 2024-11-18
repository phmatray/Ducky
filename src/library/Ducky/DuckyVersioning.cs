// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace Ducky;

/// <summary>
/// Versioning information for the Ducky library.
/// </summary>
public static class DuckyVersioning
{
    private const string DefaultVersion = "0.0.0";

    /// <summary>
    /// Get the version of the Ducky library.
    /// </summary>
    /// <returns>The version of the Ducky library.</returns>
    public static Version GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version ?? new Version(DefaultVersion);
        return assemblyVersion;
    }
}
