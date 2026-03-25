// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generator;

/// <summary>
/// A base class for source generators.
/// Provides helper methods for adding sources and other common tasks.
/// </summary>
public abstract class SourceGeneratorBase : IIncrementalGenerator
{
    /// <inheritdoc/>
    public abstract void Initialize(IncrementalGeneratorInitializationContext context);

    /// <summary>
    /// Adds a generated source file to the compilation.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="hintName">A unique hint name for the generated file.</param>
    /// <param name="source">The source code to add.</param>
    protected void AddSource(in SourceProductionContext context, string hintName, string source)
    {
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }
}
