using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using R3;
using GetStateChangedPropertyDelegate = System.Func<object, R3dux.Temp.IStateChangedNotifier>;

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

/// <summary>
/// A utility class that automatically subscribes to all <see cref="IStateChangedNotifier"/> properties
/// on a specific object
/// </summary>
public static class StateSubscriber
{
    private static readonly ConcurrentDictionary<Type, ImmutableList<GetStateChangedPropertyDelegate>> ValueDelegatesByType = new();

    /// <summary>
    /// Subscribes to all <see cref="IStateChangedNotifier"/> properties on the specified <paramref name="subject"/>
    /// to ensure <paramref name="callback"/> is called whenever a state is modified
    /// </summary>
    /// <param name="subject">The object to scan for <see cref="IStateChangedNotifier"/> properties.</param>
    /// <param name="callback">The action to execute when one of the states are modified</param>
    /// <returns></returns>
    public static IDisposable Subscribe(object subject, Action<IStateChangedNotifier> callback)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(callback);

        var subscriptions = new CompositeDisposable();

        GetStateChangedNotifierPropertyDelegatesForType(subject.GetType())
            .Select(getStateChangedNotifierPropertyValue => getStateChangedNotifierPropertyValue(subject))
            .Select(stateChangedNotifier => stateChangedNotifier.StateChanged.Subscribe(_ => callback(stateChangedNotifier)))
            .ToList()
            .ForEach(subscription => subscription.AddTo(subscriptions));

        return subscriptions;
    }

    private static IEnumerable<PropertyInfo> GetStateChangedNotifierProperties(Type t)
    {
        return t != typeof(object)
            ? GetStateChangedNotifierProperties(t.BaseType).Concat(t
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(p => typeof(IStateChangedNotifier).IsAssignableFrom(p.PropertyType)))
            : [];
    }

    private static ImmutableList<GetStateChangedPropertyDelegate> GetStateChangedNotifierPropertyDelegatesForType(Type type)
    {
        return ValueDelegatesByType.GetOrAdd(type, _ =>
        {
            var delegates = ImmutableList.CreateBuilder<GetStateChangedPropertyDelegate>();
            var stateChangedNotifierProperties = GetStateChangedNotifierProperties(type);

            foreach (var currentProperty in stateChangedNotifierProperties)
            {
                var getterMethod = typeof(Func<,>).MakeGenericType(type, currentProperty.PropertyType);
                var stronglyTypedDelegate = Delegate.CreateDelegate(getterMethod, currentProperty.GetGetMethod(true));
                var getValueDelegate = new GetStateChangedPropertyDelegate(x => (IStateChangedNotifier)stronglyTypedDelegate.DynamicInvoke(x));
                delegates.Add(getValueDelegate);
            }

            return delegates.ToImmutable();
        });
    }
}