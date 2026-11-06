using Ducky;
using Ducky.Blazor;

namespace Ducky1x.Before;

#region before-component
// Ducky 1.x: the component subscribes to its slice and dispatches through the generated Dispatcher.X() helpers.
public sealed class CounterView : DuckyComponent<CounterState>
{
    public int Value => State.Value;

    public int Renders { get; private set; }

    public void Increment() => Dispatcher.Increment(2);

    protected override void OnAfterSubscribed() => Renders++;
}
#endregion
