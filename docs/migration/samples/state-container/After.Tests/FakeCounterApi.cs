namespace StateContainerSample.After.Tests;

// Each call returns a pending task the test completes; the tokens show which runs were cancelled. By default a call
// honours its token, as a real HttpClient does, so a cancelled run ends at once; honourCancellation: false stands for an
// API that ignores it and answers late.
internal sealed class FakeCounterApi(bool honourCancellation = true) : ICounterApi
{
    private readonly List<(TaskCompletionSource<int> Result, CancellationToken Token)> _calls = [];
    private readonly List<TaskCompletionSource> _called = [];
    private readonly Lock _gate = new();

    public int Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    // Completes once the effect made call number <paramref name="call"/> (0-based).
    public Task Called(int call)
    {
        lock (_gate)
        {
            return Signal(call).Task;
        }
    }

    public CancellationToken Token(int call)
    {
        lock (_gate)
        {
            return _calls[call].Token;
        }
    }

    public void Complete(int call, int count)
    {
        lock (_gate)
        {
            _calls[call].Result.TrySetResult(count);
        }
    }

    public void Fail(int call, Exception exception)
    {
        lock (_gate)
        {
            _calls[call].Result.TrySetException(exception);
        }
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _calls.Add((result, cancellationToken));
            Signal(_calls.Count - 1).SetResult();
        }

        if (honourCancellation)
        {
            cancellationToken.Register(() => result.TrySetCanceled(cancellationToken));
        }

        return result.Task;
    }

    private TaskCompletionSource Signal(int call)
    {
        while (_called.Count <= call)
        {
            _called.Add(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return _called[call];
    }
}
