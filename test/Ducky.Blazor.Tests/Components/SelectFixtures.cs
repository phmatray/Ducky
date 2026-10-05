using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky.Blazor.Tests.Components;

// Renders a DuckySelect over the counter next to static markup, and counts its own renders (bUnit's RenderCount also
// counts its children's).
internal sealed class SelectHost : ComponentBase
{
    public int Renders { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Renders++;
        builder.AddContent(0, "count: ");
        builder.OpenComponent<DuckySelect<int>>(1);
        builder.AddComponentParameter(2, nameof(DuckySelect<>.Selector), (Func<StateSnapshot, int>)(static s => s.Get<Counter>().Value));
        builder.AddComponentParameter(3, nameof(DuckySelect<>.ChildContent), (RenderFragment<int>)(static value => b => b.AddContent(0, value)));
        builder.CloseComponent();
    }
}

// Treats every pair of values as equal, so a selection using it never reports a change.
internal sealed class AlwaysEqual<T> : IEqualityComparer<T>
{
    public bool Equals(T? x, T? y) => true;

    public int GetHashCode(T obj) => 0;
}

// A third-party base class (MudComponentBase-like): it derives from ComponentBase, so the component can't also derive
// from DuckyComponent and selects through StoreSelectionExtensions.
internal abstract class ForeignComponentBase : ComponentBase
{
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? UserAttributes { get; set; }
}

internal sealed class ForeignCounter : ForeignComponentBase, IDisposable
{
    private Selection<int> _count = null!;

    [Inject]
    private IStore Store { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    public void Dispose() => _count.Dispose();

    protected override void OnInitialized() =>
        _count = Store.Select(static s => s.Get<Counter>().Value, InvokeAsync, StateHasChanged, RendererInfo, Services);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _count.Value);
}

// Holds the store's init until the test releases it.
internal sealed class InitHold
{
    public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class HeldInit(InitHold hold) : Middleware
{
    public override ValueTask InitializeAsync(CancellationToken cancellationToken) => new(hold.Released.Task);
}
