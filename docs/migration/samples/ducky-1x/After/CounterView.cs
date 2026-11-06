using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace Ducky1x.After;

#region after-component
// Ducky 2.0: the component dispatches through Store.X() (no Dispatcher property); OnInitialized is no longer sealed.
public sealed class CounterView : DuckyComponent<CounterState>
{
    private Selection<bool>? _isPositive;

    public int Value => State.Value;

    public bool IsPositive => _isPositive!.Value;

    public int Initializations { get; private set; }

    public void Increment() => Store.Increment(2);

    protected override void OnInitialized()
    {
        _isPositive = Select(CounterSelectors.IsPositive.Create());
        Initializations++; // what OnAfterSubscribed did, after the Select calls
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Value);
}
#endregion
