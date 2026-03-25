// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageTimer
{
    private int Time
        => State.Time;

    private bool IsRunning
        => State.IsRunning;

    protected override void OnAfterSubscribed()
    {
        if (!IsRunning)
        {
            return;
        }

        StartTimer();
    }

    private void ResetTimer()
    {
        StopTimer();
        Dispatcher.ResetTimer();
    }

    private void StartTimer()
    {
        Dispatcher.StartTimer();
    }

    private void StopTimer()
    {
        Dispatcher.StopTimer();
    }
}
