using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.5 (EffectContextExtensions.Run, EFF-02), §6.6 (an OperationCanceledException is cancellation iff it is for
// the run's own token; anything else is a failure); INV-11.
public sealed class EffectContextRunTests
{
    // HttpClient's own timeout cancels a token that is not the run's, and the run's token stays live: Run dispatches
    // failed(ex) and the run ends normally, so no EffectFailed.
    [Fact]
    public async Task EffectContext_Run_HttpTimeout_DispatchesFailed()
    {
        using var http = new HttpClient(new SilentHandler()) { Timeout = TimeSpan.FromMilliseconds(1) };
        var effect = new Handler<Load>((load, context, token) => context.Run(
            new RunStarted(load.Id),
            work => http.GetStringAsync(new Uri("http://ducky.test/"), work),
            body => new Loaded(body.Length),
            ex => new RunFailed(ex),
            token));
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        var actions = store.State.Get<Seen>().Actions;
        actions.Take(2).ShouldBe([new Load(1), new RunStarted(1)]);
        actions.Count.ShouldBe(3);
        actions[2].ShouldBeOfType<RunFailed>().Exception.ShouldBeOfType<TaskCanceledException>()
            .InnerException.ShouldBeOfType<TimeoutException>();
        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeFalse();
    }

    // Non-normative: started, then work with the caller's token, then succeeded(result).
    [Fact]
    public async Task EffectContext_Run_WorkSucceeds_DispatchesStartedThenSucceeded()
    {
        CancellationToken runToken = default;
        CancellationToken workToken = default;
        var effect = new Handler<Load>((load, context, token) =>
        {
            runToken = token;
            return context.Run(
                new RunStarted(load.Id),
                work =>
                {
                    workToken = work;
                    return Task.FromResult(load.Id * 10);
                },
                result => new Loaded(result),
                ex => new RunFailed(ex),
                token);
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(4));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(4), new RunStarted(4), new Loaded(40)]);
        workToken.ShouldBe(runToken);
    }

    // Non-normative: any other exception from the work, thrown before or after its first await, dispatches failed(ex).
    [Fact]
    public async Task EffectContext_Run_WorkThrows_DispatchesFailed()
    {
        var sync = new InvalidOperationException("sync");
        var faulted = new HttpRequestException("async");
        var effect = new Handler<Load>((load, context, token) => context.Run(
            new RunStarted(load.Id),
            work => load.Id == 1 ? throw sync : Task.FromException<int>(faulted),
            result => new Loaded(result),
            ex => new RunFailed(ex),
            token));
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<Seen>().Actions.ShouldBe(
            [new Load(1), new RunStarted(1), new RunFailed(sync), new Load(2), new RunStarted(2), new RunFailed(faulted)]);
    }

    // Non-normative: a Switch supersession cancels run 1's own token while its work waits. The cancellation propagates out
    // of Run as cancellation: failed is never called, Run's task is canceled, and no EffectFailed is dispatched. Run 1's
    // dispatches are dropped once its token is cancelled, so only failed's calls and Run's task can show a swallowed
    // cancellation.
    [Fact]
    public async Task EffectContext_Run_OwnTokenCancelled_PropagatesWithoutAction()
    {
        var never = new TaskCompletionSource<int>();
        var failedCalls = 0;
        Task? run1 = null;
        var effect = new SwitchHandler<Load>((load, context, token) =>
        {
            var run = context.Run(
                new RunStarted(load.Id),
                work => load.Id == 1 ? never.Task.WaitAsync(work) : Task.FromResult(load.Id),
                result => new Loaded(result),
                ex =>
                {
                    failedCalls++;
                    return new RunFailed(ex);
                },
                token);
            run1 ??= run;
            return run;
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new RunStarted(1), new Load(2), new RunStarted(2), new Loaded(2)]);
        failedCalls.ShouldBe(0);
        run1.ShouldNotBeNull().IsCanceled.ShouldBeTrue();
    }

    // Non-normative: an OperationCanceledException that carries the run's own token is cancellation even before that token
    // is cancelled (§6.6: ex.CancellationToken == runToken || runToken.IsCancellationRequested).
    [Fact]
    public async Task EffectContext_Run_OceForOwnToken_IsCancellation()
    {
        var effect = new Handler<Load>((load, context, token) => context.Run<int>(
            new RunStarted(load.Id),
            work => throw new OperationCanceledException(work),
            result => new Loaded(result),
            ex => new RunFailed(ex),
            token));
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new RunStarted(1)]);
        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeFalse();
    }

    // Non-normative: a null argument is a programmer error, thrown synchronously before anything is dispatched.
    [Fact]
    public async Task EffectContext_Run_NullArgument_ThrowsSynchronously()
    {
        EffectContext? captured = null;
        var effect = new Handler<Load>((_, context, _) =>
        {
            captured = context;
            return Task.CompletedTask;
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);
        store.Dispatch(new Load(1));
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        var context = captured.ShouldNotBeNull();
        Func<CancellationToken, Task<int>> work = _ => Task.FromResult(1);
        Func<int, object> succeeded = result => new Loaded(result);
        Func<Exception, object> failed = ex => new RunFailed(ex);
        var ct = CancellationToken.None;

        Should.Throw<ArgumentNullException>(() => EffectContextExtensions.Run(null!, new RunStarted(1), work, succeeded, failed, ct)).ParamName.ShouldBe("context");
        Should.Throw<ArgumentNullException>(() => context.Run(null!, work, succeeded, failed, ct)).ParamName.ShouldBe("started");
        Should.Throw<ArgumentNullException>(() => context.Run(new RunStarted(1), null!, succeeded, failed, ct)).ParamName.ShouldBe("work");
        Should.Throw<ArgumentNullException>(() => context.Run(new RunStarted(1), work, null!, failed, ct)).ParamName.ShouldBe("succeeded");
        Should.Throw<ArgumentNullException>(() => context.Run(new RunStarted(1), work, succeeded, null!, ct)).ParamName.ShouldBe("failed");
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1)]);
    }
}
