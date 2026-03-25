// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Generator.Core;

/// <summary>
/// Options for configuring the ActionDispatcherGenerator.
/// </summary>
public class ActionDispatcherGeneratorOptions
{
    /// <summary>
    /// Gets or sets the name of the action to generate a dispatcher method for.
    /// </summary>
    public string ActionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the fully qualified name of the action type.
    /// </summary>
    public string ActionFullyQualifiedName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of parameters for the action constructor.
    /// </summary>
    public List<ParameterDescriptor> Parameters { get; set; } = new();
}