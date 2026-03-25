// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

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
