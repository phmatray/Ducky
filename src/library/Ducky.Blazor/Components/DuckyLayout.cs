// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor;

/// <summary>
/// A base layout class for Ducky components that manages state and dispatches actions.
/// </summary>
/// <typeparam name="TState">The type of the state managed by this component.</typeparam>
public abstract class DuckyLayout<TState> : DuckyComponent<TState>
    where TState : notnull
{
    /// <summary>
    /// Gets or sets the content to be rendered inside the layout.
    /// </summary>
    [Parameter]
    public RenderFragment? Body { get; set; }

    /// <inheritdoc />
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LayoutComponentBase))]
    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters).ConfigureAwait(false);
    }
}
