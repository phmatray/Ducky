// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Components.Pages;

public partial class Home
{
    private bool _disposed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Store.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Store.StateChanged -= OnStateChanged;
        }

        _disposed = true;
    }
}
