using TimeWarp.State;

namespace TimeWarpSample.Before;

#region before-state
// TimeWarp.State: a mutable State<T> with private setters; Initialize() sets the initial values.
public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

public sealed partial class CounterState : State<CounterState>
{
    public int Count { get; private set; }

    public bool IsLoading { get; private set; }

    public string? Error { get; private set; }

    public override void Initialize()
    {
        Count = 0;
        IsLoading = false;
        Error = null;
    }
}
#endregion

#region before-increment
// TimeWarp.State: an action set nests the action and its handler in the state; the handler mutates the state in place.
public sealed partial class CounterState
{
    public static class IncrementCountActionSet
    {
        public sealed class Action : IAction
        {
            public Action(int amount)
            {
                Amount = amount;
            }

            public int Amount { get; }
        }

        public sealed class Handler : ActionHandler<Action>
        {
            public Handler(IStore store) : base(store)
            {
            }

            private CounterState CounterState => Store.GetState<CounterState>();

            public override Task Handle(Action action, CancellationToken cancellationToken)
            {
                CounterState.Count += action.Amount;
                return Task.CompletedTask;
            }
        }
    }
}
#endregion

#region before-load
// TimeWarp.State: one async handler both awaits the API and mutates the state, before and after the await.
public sealed partial class CounterState
{
    public static class LoadCountActionSet
    {
        public sealed class Action : IAction;

        public sealed class Handler : ActionHandler<Action>
        {
            private readonly ICounterApi _api;

            public Handler(IStore store, ICounterApi api) : base(store)
            {
                _api = api;
            }

            private CounterState CounterState => Store.GetState<CounterState>();

            public override async Task Handle(Action action, CancellationToken cancellationToken)
            {
                CounterState.IsLoading = true;
                CounterState.Error = null;
                try
                {
                    CounterState.Count = await _api.GetCountAsync(cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    CounterState.Error = exception.Message;
                }

                CounterState.IsLoading = false;
            }
        }
    }
}
#endregion
