using MudBlazor.Utilities;

namespace Demo.BlazorWasm.Components.Shared;

public partial class AnalogClock
{
    private string ClockHandStyle
        => new StyleBuilder()
            .AddStyle("rotate", State.SelectAngle())
            .AddStyle("transform-origin", "50% 50%")
            .AddStyle("transition", "transform 1s")
            .Build();
}
