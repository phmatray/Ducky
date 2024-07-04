using R3;

namespace R3dux.Temp;

public interface IStateChangedNotifier
{
    Observable<Unit> StateChanged { get; }
}

public interface IState<TState> : IStateChangedNotifier
{
    TState Value { get; }
    new Observable<TState> StateChanged { get; }
}

public delegate TValue Selector<in TState, out TValue>(TState state);
public delegate bool ValueEquals<in TValue>(TValue value1, TValue value2);
public delegate void SelectedValueChanged<in TValue>(TValue value);

public interface IStateSelector<out TState, TValue> : IState<TValue>
{
    void Select(
        Selector<TState, TValue> selector,
        ValueEquals<TValue>? valueEquals = null,
        SelectedValueChanged<TValue>? selectedValueChanged = null);

    Observable<TValue> SelectedValueChanged { get; }
}
