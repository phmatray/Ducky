using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageErrors
{
    private string _searchTerm = string.Empty;

    private ValueCollection<Notification> ErrorNotifications
        => State.SelectErrorNotifications();

    private void ThrowException()
    {
        throw new InvalidOperationException("This is a test exception thrown from the UI!");
    }

    private async Task ThrowAsyncExceptionAsync()
    {
        await Task.Delay(100);
        throw new NotSupportedException("This is an async exception for testing error boundaries!");
    }

    private void SimulateActionError()
    {
        // Dispatch an action that will fail
        Dispatcher.TestErrorAction();
    }

    private void ClearErrors()
    {
        Dispatcher.ClearErrorNotifications();
    }
}
