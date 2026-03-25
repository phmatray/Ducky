// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageCounter
{
    private int Amount { get; set; } = 2;

    private int CounterValue
        => State.Value;

    private bool IsDisabled
        => State.Value <= 0;

    private bool IsEffectTriggered
        => State.Value > 15;

    private void Increment()
        => Dispatcher.Increment(Amount);

    private void Decrement()
        => Dispatcher.Decrement(Amount);

    private void Reset()
        => Dispatcher.SetValue(10);
}
