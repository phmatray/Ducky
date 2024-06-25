using System.Reactive.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace BlazorAppRxStore.SeedWork;

public class RxComponentBase<TState, TReducer>
    : ComponentBase, IDisposable
    where TReducer : ReducerBase<TState>, new()
{
    private readonly List<IDisposable> _subscriptions = [];
    
    [Inject]
    protected RxStore<TState, TReducer> Store { get; init; } = default!;
    
    protected async Task<T> SubscribeToStateAsync<T>(Func<TState, T> selector, string fieldName)
    {
        var initialValue = await Store.State
            .Select(selector)
            .FirstAsync();

        SetFieldValue(fieldName, initialValue);

        var subscription = Store
            .Select(selector)
            .Subscribe(value =>
            {
                SetFieldValue(fieldName, value);
                InvokeAsync(StateHasChanged);
            });

        _subscriptions.Add(subscription);

        return initialValue;
    }

    private void SetFieldValue<T>(string fieldName, T value)
    {
        var field = GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

        if (field == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found on '{GetType().Name}'.");
        }

        field.SetValue(this, value);
    }

    protected void Dispatch(IAction action)
        => Store.Dispatch(action);
    
    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }
}