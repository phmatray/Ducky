// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace R3dux;

/// <summary>
/// Options for configuring R3dux services.
/// </summary>
public class R3duxOptions
{
    /// <summary>
    /// Gets or sets the assemblies to scan for slices and effects.
    /// </summary>
    public string[] AssemblyNames { get; set; } = [];

    /// <summary>
    /// Gets the assemblies to scan for slices and effects. Defaults to the executing assembly.
    /// </summary>
    public Assembly[] Assemblies
        => GetAssemblies();

    private static Assembly[] GetDefaultAssemblies()
    {
        var entryAssembly =
            Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to determine the entry assembly.");

        return [entryAssembly];
    }

    private Assembly[] GetAssemblies()
    {
        if (AssemblyNames.Length == 0)
        {
            return GetDefaultAssemblies();
        }

        return AssemblyNames
            .Select(Assembly.Load)
            .ToArray();
    }
}
