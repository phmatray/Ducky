// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace R3dux;

/// <summary>
/// Versioning information for the R3dux library.
/// </summary>
public static class R3duxVersioning
{
    private const string DefaultVersion = "0.0.0";

    /// <summary>
    /// Get the version of the R3dux library.
    /// </summary>
    /// <returns>The version of the R3dux library.</returns>
    public static Version GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version ?? new Version(DefaultVersion);
        return assemblyVersion;
    }
}
