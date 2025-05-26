using Ducky.Middlewares.AsyncEffect;

namespace Demo.ConsoleApp.Counter;

public sealed class DelayedIncrementEffect : AsyncEffect<IncrementAsync>
{
    public override async Task HandleAsync(IncrementAsync action, IRootState rootState)
    {
        Console.WriteLine($"[Effect] Starting delayed increment of {action.Amount} after {action.DelayMs}ms...");

        await Task.Delay(action.DelayMs).ConfigureAwait(false);

        Console.WriteLine($"[Effect] Delay complete, incrementing by {action.Amount}");
        Dispatcher.Increment(action.Amount);
    }
}

public sealed class CounterThresholdEffect : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        if (counterState.Value >= 10)
        {
            Console.WriteLine("[Effect] Counter reached 10! Triggering celebration...");
            await Task.Delay(500).ConfigureAwait(false);
            Console.WriteLine("[Effect] 🎉 Congratulations! You've reached 10!");
        }

        if (counterState.Value < 20)
        {
            return;
        }

        Console.WriteLine("[Effect] Counter is getting high! Consider resetting.");
    }
}
