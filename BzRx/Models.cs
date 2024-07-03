namespace BzRx;

public interface IAction
{
    string Type { get; }
}

public class RxAction<T> where T : class
{
    public virtual string Type { get; set; }
}

public delegate TState ActionReducer<TState, in TAction>(
    TState state, TAction action)
    where TAction : IAction;

public delegate ActionReducer<TState, TAction> ActionReducerFactory<TState, TAction>(
    ActionReducerMap<TState, TAction> reducerMap,
    InitialState<TState>? initialState = null)
    where TAction : IAction;

public class ActionReducerMap<TState, TAction>
    : Dictionary<string, ActionReducer<TState, TAction>>
    where TAction : IAction;

public delegate ActionReducer<TState, TAction> MetaReducer<TState, TAction>(
    ActionReducer<TState, TAction> reducer)
    where TAction : IAction;

public class InitialState<T>
{
    public Func<T> State { get; set; }
}

public class StoreFeature<TState, TAction>
    where TAction : IAction
{
    public string Key { get; set; }
    public ActionReducerMap<TState, TAction> Reducers { get; set; }
    public ActionReducerFactory<TState, TAction> ReducerFactory { get; set; }
    public InitialState<TState> InitialState { get; set; }
    public List<MetaReducer<TState, TAction>> MetaReducers { get; set; }
}

public delegate TResult Selector<in TState, out TResult>(TState state);

[Obsolete("Selectors with props are deprecated, for more info see https://github.com/ngrx/platform/issues/2980")]
public delegate TResult SelectorWithProps<in TState, in TProps, out TResult>(TState state, TProps props);
public class RuntimeChecks
{
    public bool StrictStateSerializability { get; set; }
    public bool StrictActionSerializability { get; set; }
    public bool StrictStateImmutability { get; set; }
    public bool StrictActionImmutability { get; set; }
    public bool StrictActionWithinNgZone { get; set; }
    public bool? StrictActionTypeUniqueness { get; set; }
}

public class SelectSignalOptions<T>
{
    public Func<T, T, bool> Equal { get; set; }
}

public class NotAllowedCheck
{
    public static string ArraysAreNotAllowedMsg = "action creator cannot return an array";
    public static string TypePropertyIsNotAllowedMsg = "action creator cannot return an object with a property named `type`";
    public static string EmptyObjectsAreNotAllowedMsg = "action creator cannot return an empty object";
    public static string ArraysAreNotAllowedInProps = "action creator props cannot be an array";
    public static string TypePropertyIsNotAllowedInProps = "action creator props cannot have a property named `type`";
    public static string EmptyObjectsAreNotAllowedInProps = "action creator props cannot be an empty object";
    public static string PrimitivesAreNotAllowedInProps = "action creator props cannot be a primitive value";
}

public delegate R FunctionWithParametersType<P, R>(params P[] args);

public interface ActionCreator<T, C>
    where C : Delegate
{
    C Creator { get; }
    string Type { get; }
}

public class ActionCreatorProps<T>
    where T : class
{
    public string As => "props";
    public T Props { get; set; }
}
