// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Generator.Core;

/// <summary>
/// Generates profiling code for performance monitoring and analysis.
/// </summary>
public class ProfilingGenerator : SourceGeneratorBase<ProfilingGeneratorOptions>
{
    /// <summary>
    /// Builds the model representing the compilation unit for profiling generation.
    /// </summary>
    /// <param name="opts">The options containing configuration for profiling generation.</param>
    /// <returns>A <see cref="CompilationUnitElement"/> representing the generated code structure.</returns>
    protected override CompilationUnitElement BuildModel(ProfilingGeneratorOptions opts)
    {
        return new()
        {
            Usings = new List<string> { "System" },
            Namespaces = new List<NamespaceElement> { new() { Name = "MyNameSpace" } }
        };
    }
}
