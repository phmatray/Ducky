namespace Demo.BlazorWasm.Components.Pages;

public partial class PageTimer
{
    private int Time
        => State.Time;

    private bool IsRunning
        => State.IsRunning;

    protected override void OnAfterSubscribed()
    {
        if (!IsRunning)
        {
            return;
        }

        StartTimer();
    }

    private void ResetTimer()
    {
        StopTimer();
        Dispatcher.ResetTimer();
    }

    private void StartTimer()
    {
        Dispatcher.StartTimer();
    }

    private void StopTimer()
    {
        Dispatcher.StopTimer();
    }
}
