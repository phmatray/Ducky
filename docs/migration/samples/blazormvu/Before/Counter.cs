using BlazorMVU;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace BlazorMvu.Before;

public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

#region before-model
// BlazorMVU: the model and its messages belong to one component.
public sealed record CounterModel(int Count, bool IsLoading, string? Error, int Seconds);

public abstract record CounterMsg
{
    public sealed record Increment : CounterMsg;

    public sealed record Decrement : CounterMsg;

    public sealed record Load : CounterMsg;

    public sealed record Loaded(int Count) : CounterMsg;

    public sealed record LoadFailed(string Error) : CounterMsg;

    public sealed record Tick : CounterMsg;
}
#endregion

public sealed class Counter : MvuComponent<CounterModel, CounterMsg>
{
    [Inject]
    public required ICounterApi Api { get; set; }

    #region before-update
    // BlazorMVU: Init gives the first model; one Update switch handles every message.
    protected override CounterModel Init() => new(Count: 0, IsLoading: false, Error: null, Seconds: 0);

    protected override CounterModel Update(CounterMsg msg, CounterModel model) => msg switch
    {
        CounterMsg.Increment => model with { Count = model.Count + 1 },
        CounterMsg.Decrement => model with { Count = model.Count - 1 },
        CounterMsg.Loaded loaded => model with { Count = loaded.Count, IsLoading = false },
        CounterMsg.LoadFailed failed => model with { IsLoading = false, Error = failed.Error },
        CounterMsg.Tick => model with { Seconds = model.Seconds + 1 },
        _ => model,
    };
    #endregion

    #region before-cmd
    // BlazorMVU: Cmd.OfTask runs the I/O and maps its result (or its exception) to a message.
    protected override (CounterModel Model, Cmd<CounterMsg> Cmd) UpdateWithCmd(CounterMsg msg, CounterModel model) =>
        msg is CounterMsg.Load
            ? (model with { IsLoading = true, Error = null }, Cmd.OfTask<CounterMsg>(LoadAsync))
            : (Update(msg, model), Cmd.None<CounterMsg>());

    private async Task<CounterMsg> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new CounterMsg.Loaded(await Api.GetCountAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return new CounterMsg.LoadFailed(exception.Message);
        }
    }
    #endregion

    #region before-sub
    // BlazorMVU: Sub.Every ticks while the component lives.
    protected override Sub<CounterMsg> Subscriptions(CounterModel model) =>
        Sub.Every<CounterMsg>(TimeSpan.FromSeconds(1), new CounterMsg.Tick(), "clock");
    #endregion

    #region before-component
    public void Increment() => Dispatch(new CounterMsg.Increment());

    public void Reload() => Dispatch(new CounterMsg.Load());

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Count);
    #endregion
}
