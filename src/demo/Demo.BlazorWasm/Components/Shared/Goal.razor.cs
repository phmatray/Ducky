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
