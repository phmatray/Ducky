using Microsoft.AspNetCore.Components;

namespace StateContainerSample.Before;

#region before-store
// A hand-written state container, as in Microsoft's Blazor state management docs: properties, methods that change them,
// and an OnChange event. [PersistentState] (.NET 10) carries the prerendered values into the interactive render.
public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

public sealed class CounterStore(ICounterApi api)
{
    [PersistentState]
    public int Count { get; set; }

    public bool IsLoading { get; private set; }

    public string? Error { get; private set; }

    public event Action? OnChange;

    public void Increment(int amount)
    {
        Count += amount;
        NotifyStateChanged();
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        Error = null;
        NotifyStateChanged();
        try
        {
            Count = await api.GetCountAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Error = exception.Message;
        }

        IsLoading = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
#endregion
