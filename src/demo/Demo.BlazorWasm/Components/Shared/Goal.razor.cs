// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class Goal
{
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    [EditorRequired]
    public Func<bool> Condition { get; set; } = () => false;

    [Parameter]
    [EditorRequired]
    public string ConditionKey { get; set; } = string.Empty;

    private bool IsConditionMet
        => State.SelectIsGoalMet(ConditionKey) || Condition();

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (!Condition())
        {
            return;
        }

        Dispatcher.SetGoalMet(ConditionKey);
    }

    private static Color GetGoalColor(bool conditionMet)
        => conditionMet ? Color.Success : Color.Inherit;
}
