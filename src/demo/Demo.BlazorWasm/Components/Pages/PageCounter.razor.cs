namespace Demo.BlazorWasm.Components.Pages;

public partial class PageCounter
{
    private int Amount { get; set; } = 2;

    private int CounterValue
        => State.Value;

    private bool IsDisabled
        => State.Value <= 0;

    private bool IsEffectTriggered
        => State.Value > 15;

    private void Increment()
        => Dispatcher.Increment(Amount);

    private void Decrement()
        => Dispatcher.Decrement(Amount);

    private void Reset()
        => Dispatcher.SetValue(10);
}
