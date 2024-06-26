using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace BlazorStore;

public class RxComponentBase<TState, TReducer>
    : ComponentBase, IDisposable
    where TReducer : IActionReducer<TState>, new()
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dictionary<string, Action<object>> _fieldSetters = new();
    
    [Inject]
    protected RxStore<TState, TReducer> Store { get; init; } = default!;
    
    protected void SubscribeToState<T>(
        Func<TState, T> selector,
        object? argument, 
        [CallerArgumentExpression("argument")] string? fieldName = null)
    {
        ArgumentNullException.ThrowIfNull(fieldName);

        if (!_fieldSetters.TryGetValue(fieldName, out var setter))
        {
            setter = CreateFieldSetter(fieldName);
            _fieldSetters[fieldName] = setter;
        }
        
        var subscription = Store
            .Select(selector)
            .Subscribe(value =>
            {
                setter(value!);
                InvokeAsync(StateHasChanged);
            });
    
        _subscriptions.Add(subscription);
    }

    private Action<object> CreateFieldSetter(string fieldName)
    {
        var field =
            GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new ArgumentException($"Field '{fieldName}' not found on '{GetType().Name}'.");

        return value => field.SetValue(this, value);
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