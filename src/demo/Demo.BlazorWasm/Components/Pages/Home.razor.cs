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
