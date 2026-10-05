using System.Collections.Immutable;
using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor.Tests.Components;

internal sealed record SetItem(int Index, string Value);

internal sealed record Items(ImmutableArray<string> Values);

internal sealed class ItemsSlice : Slice<Items>
{
    public ItemsSlice() => On<SetItem>((state, action) => state with { Values = state.Values.SetItem(action.Index, action.Value) });

    protected override Items Initial => new(["a", "b"]);
}

// Shows the item at Index through one selection that reads the parameter: a parameter change shows without a dispatch.
internal sealed class ItemView : DuckyComponent
{
    private Selection<string> _item = null!;

    [Parameter]
    public int Index { get; set; }

    public Selection<string> SelectNow() => Select((Items s) => s.Values[0]);

    protected override void OnInitialized() => _item = Select((Items s) => s.Values[Index]);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _item.Value);
}

// Overrides both after-render hooks without calling base, and tries to Select from each.
internal sealed class SkipsBase : DuckyComponent
{
    private Selection<int> _count = null!;

    public List<string> Seen { get; } = [];

    protected override void OnInitialized() => _count = Select(state => state.Get<Counter>().Value);

    protected override void OnAfterRender(bool firstRender)
    {
        Seen.Add($"sync {firstRender}");
        Seen.Add(Try(() => Select(state => state.Get<Counter>().Value)));
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        Seen.Add($"async {firstRender}");
        Seen.Add(Try(() => Select((Counter c) => c.Value)));
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _count.Value);

    private static string Try(Action select)
    {
        try
        {
            select();
            return "selected";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message[..8];
        }
    }
}

// Four selections that one Increment changes together; the first counts its evaluations.
internal sealed class FourCounts : DuckyComponent
{
    private readonly List<Selection<int>> _counts = [];

    public int Evaluations { get; private set; }

    protected override void OnInitialized()
    {
        _counts.Add(Select(state =>
        {
            Evaluations++;
            return state.Get<Counter>().Value;
        }));
        _counts.Add(Select((Counter c) => c.Value * 2));
        _counts.Add(Select((Counter c) => -c.Value));
        _counts.Add(Select((Counter c) => c.Value + 1));
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, string.Join(",", _counts.Select(count => count.Value)));
}

// Reads State only while Show is set, so its first read can come after the first render.
internal sealed class ConditionalCounter : DuckyComponent<Counter>
{
    [Parameter]
    public bool Show { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Show)
        {
            builder.AddContent(0, State.Value);
        }
    }
}

// Reads State on every render, and dispatches through the component.
internal sealed class CounterView : DuckyComponent<Counter>
{
    public void Increment() => Dispatch(new Increment());

    public Task<DispatchResult> IncrementAsync() => DispatchAsync(new Increment());

    public IStore Injected => Store;

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Value);
}

// Two buttons whose onclick handlers dispatch, one with Dispatch and one with await DispatchAsync.
internal sealed class ClickCounter : DuckyComponent<Counter>
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "id", "sync");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => Dispatch(new Increment())));
        builder.CloseElement();
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "id", "async");
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, async () => await DispatchAsync(new Increment())));
        builder.CloseElement();
        builder.OpenElement(6, "span");
        builder.AddContent(7, State.Value);
        builder.CloseElement();
    }
}

// Dispatches Increment after its first render: rendered before a CounterView in one batch, it commits between that
// view's render and its after-render hook.
internal sealed class IncrementsAfterFirstRender : ComponentBase
{
    [Inject]
    private IStore Store { get; set; } = null!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            Store.Dispatch(new Increment());
        }
    }
}

// DuckyLayout has the same members as DuckyComponent.
internal sealed class CounterLayout : DuckyLayout
{
    private Selection<int> _count = null!;
    private Selection<string> _item = null!;

    public List<bool> FirstRenders { get; } = [];

    public IStore Injected => Store;

    public Selection<int> SelectNow() => Select(state => state.Get<Counter>().Value);

    public void DisposeFromFinalizer() => Dispose(disposing: false);

    public void Increment() => Dispatch(new Increment());

    public Task<DispatchResult> IncrementAsync() => DispatchAsync(new Increment());

    protected override void OnInitialized()
    {
        _count = Select(state => state.Get<Counter>().Value);
        _item = Select((Items s) => s.Values[0]);
    }

    protected override void OnAfterRender(bool firstRender) => FirstRenders.Add(firstRender);

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        FirstRenders.Add(firstRender);
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, $"{_count.Value}{_item.Value}");
}

// Overrides Dispose(bool) and calls base: the subscription goes with it.
internal sealed class DisposingView : DuckyComponent
{
    private Selection<int> _count = null!;

    public int Disposals { get; private set; }

    public void DisposeFromFinalizer() => Dispose(disposing: false);

    protected override void OnInitialized() => _count = Select(state => state.Get<Counter>().Value);

    protected override void Dispose(bool disposing)
    {
        Disposals++;
        base.Dispose(disposing);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _count.Value);
}

// A selector that throws once in the change check when armed, and reads normally in the render that follows.
internal sealed class ArmedView : DuckyComponent
{
    private Selection<int> _count = null!;

    public bool Armed { get; set; }

    protected override void OnInitialized() => _count = Select(state =>
    {
        if (Armed)
        {
            Armed = false;
            throw new InvalidOperationException("selector");
        }

        return state.Get<Counter>().Value;
    });

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _count.Value);
}

// Passes null selectors through to the protected overloads.
internal sealed class NullSelectors : DuckyComponent
{
    public void SelectSnapshot() => Select<int>(null!);

    public void SelectState() => Select<Counter, int>(null!);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}

internal sealed class NullSelectorsLayout : DuckyLayout
{
    public void SelectSnapshot() => Select<int>(null!);

    public void SelectState() => Select<Counter, int>(null!);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
    }
}

internal sealed class Outer
{
    internal sealed class Generic<T> : DuckyComponent
    {
        public Selection<int> SelectNow() => Select(state => state.Get<Counter>().Value);

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
        }
    }
}

// Implements IAsyncDisposable, so the renderer calls only DisposeAsync; it calls Dispose, the documented pattern.
internal sealed class AsyncDisposableView : DuckyComponent<Counter>, IAsyncDisposable
{
    public int Evaluations { get; private set; }

    public bool DisposedAsync { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposedAsync = true;
        Dispose();
        return ValueTask.CompletedTask;
    }

    protected override void OnInitialized() => Select(state =>
    {
        Evaluations++;
        return state.Get<Counter>().Value;
    });

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Value);
}

// Renders an AsyncDisposableView while Show is true, so removing it lets the renderer dispose it.
internal sealed class AsyncDisposableHost : ComponentBase
{
    [Parameter]
    public bool Show { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Show)
        {
            builder.OpenComponent<AsyncDisposableView>(0);
            builder.CloseComponent();
        }
    }
}
