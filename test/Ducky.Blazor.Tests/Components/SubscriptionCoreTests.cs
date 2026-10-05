using System.Collections.Concurrent;
using Ducky.Blazor.Tests.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using RendererDispatcher = Microsoft.AspNetCore.Components.Dispatcher;

namespace Ducky.Blazor.Tests.Components;

// SPEC §11.2 (the subscription core), D9, ADR-0028; INV-19.
public sealed class SubscriptionCoreTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Schedule_InvokeAsyncThrows_FlagReset()
    {
        // A synchronous throw from invokeAsync, before Evaluate started, resets the flag (logged), so the next commit
        // schedules again instead of waiting forever for an Evaluate that will never run.
        await using var host = await Host.CreateAsync();
        var failure = new InvalidOperationException("renderer unavailable");
        var calls = 0;
        using var core = host.Core(work => ++calls == 1 ? throw failure : work());
        using var count = core.Select(Count);
        count.Value.ShouldBe(0);

        host.Store.Dispatch(new Increment());
        host.Store.Dispatch(new Increment());
        await host.Rendered.Task.WaitAsync(_bound, TestContext.Current.CancellationToken);

        calls.ShouldBe(2);
        host.Renders.ShouldBe(1);
        host.Logs().ShouldBe([(2001, LogLevel.Warning, Host.ScheduleFailedMessage, failure)]);
    }

    public static TheoryData<string> Faults() => ["faulted", "cancelled", "faulted later"];

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task Schedule_InvokeAsyncTaskFaults_FlagReset(string fault)
    {
        // A rejected dispatcher task (before Evaluate started) resets the flag the same way, whenever it completes.
        await using var host = await Host.CreateAsync();
        var failure = new InvalidOperationException("dispatcher rejected the work");
        var later = new TaskCompletionSource();
        var calls = 0;
        using var core = host.Core(work => ++calls switch
        {
            1 when fault == "faulted" => Task.FromException(failure),
            1 when fault == "cancelled" => Task.FromCanceled(new CancellationToken(canceled: true)),
            1 => later.Task,
            _ => work(),
        });
        using var count = core.Select(Count);
        count.Value.ShouldBe(0);

        host.Store.Dispatch(new Increment());
        later.TrySetException(failure);
        host.Store.Dispatch(new Increment());
        await host.Rendered.Task.WaitAsync(_bound, TestContext.Current.CancellationToken);

        calls.ShouldBe(2);
        host.Renders.ShouldBe(1);
        var log = host.Logs().ShouldHaveSingleItem();
        (log.Id, log.Level, log.Message).ShouldBe((2001, LogLevel.Warning, Host.ScheduleFailedMessage));
        if (fault == "cancelled")
        {
            log.Exception.ShouldBeOfType<TaskCanceledException>();
        }
        else
        {
            log.Exception.ShouldBeSameAs(failure);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Schedule_RendererDisposed_Swallowed(bool faulted)
    {
        // A torn-down renderer throws ObjectDisposedException: swallowed without a log, and nothing is scheduled again.
        await using var host = await Host.CreateAsync();
        var calls = 0;
        using var core = host.Core(_ =>
        {
            calls++;
            var disposed = new ObjectDisposedException("renderer");
            return faulted ? Task.FromException(disposed) : throw disposed;
        });
        using var count = core.Select(Count);

        host.Store.Dispatch(new Increment());
        host.Store.Dispatch(new Increment());

        calls.ShouldBe(1);
        host.Renders.ShouldBe(0);
        host.Logs().ShouldBeEmpty();
    }

    [Fact]
    public async Task Schedule_DoesNotFlowCausalScope()
    {
        // The drainer runs off the renderer while the renderer is busy, so InvokeAsync posts. ExecutionContext.SuppressFlow
        // keeps the drainer's causal scope (and any other AsyncLocal) out of Evaluate and of everything the render does:
        // an action dispatched from StateHasChanged starts its own chain.
        await using var host = await Host.CreateAsync();
        var renderer = RendererDispatcher.CreateDefault();
        var ambient = new AsyncLocal<string>();
        string? seen = "unset";
        using var core = host.Core(renderer.InvokeAsync, () =>
        {
            seen = ambient.Value;
            host.Store.Dispatch(new Probe());
        });
        using var count = core.Select(Count);
        count.Value.ShouldBe(0);

        await renderer.InvokeAsync(() =>
        {
            // Inside a renderer work item (busy) but on a flow that is not the renderer's: CheckAccess is false.
            var rendererContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                renderer.CheckAccess().ShouldBeFalse();
                ambient.Value = "drainer";
                host.Store.Dispatch(new Increment());
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(rendererContext);
            }
        });
        await host.Rendered.Task.WaitAsync(_bound, TestContext.Current.CancellationToken);

        seen.ShouldBeNull();
        host.ShouldStartOwnChain<Probe>();
    }

    [Fact]
    public async Task Schedule_DrainerOnRendererThread_InlineInvoke_DoesNotFlowCausalScope()
    {
        // The drainer is already on the renderer's sync context (an event handler), so InvokeAsync runs the delegate
        // inline with the drainer's causal scope current. The await Task.Yield() inside the suppressed region posts
        // Evaluate with an empty ExecutionContext: it runs after the drain, outside its scope.
        await using var host = await Host.CreateAsync();
        var renderer = RendererDispatcher.CreateDefault();
        var ambient = new AsyncLocal<string>();
        string? seen = "unset";
        var inline = false;
        using var core = host.Core(
            work =>
            {
                inline = renderer.CheckAccess();
                return renderer.InvokeAsync(work);
            },
            () =>
            {
                seen = ambient.Value;
                host.Store.Dispatch(new Probe());
            });
        using var count = core.Select(Count);
        count.Value.ShouldBe(0);

        await renderer.InvokeAsync(() =>
        {
            ambient.Value = "event handler";
            host.Store.Dispatch(new Increment());
        });
        await host.Rendered.Task.WaitAsync(_bound, TestContext.Current.CancellationToken);

        inline.ShouldBeTrue();
        seen.ShouldBeNull();
        host.ShouldStartOwnChain<Probe>();
    }

    [Fact]
    public async Task Evaluate_SelectorThrows_AtMostOnePendingInvoke()
    {
        // Evaluate resets the flag first; a commit during its run schedules the next one. A selector that then throws is
        // caught and logged inside Evaluate (treated as changed), so the task never faults and nothing clears the newer
        // commit's flag: a third commit finds one InvokeAsync pending and schedules no second one.
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        var failure = new InvalidOperationException("selector");
        var thrown = 0;
        using var core = host.Core(renderer.InvokeAsync);
        using var count = core.Select(state =>
        {
            var value = Count(state);
            if (value == 1 && Interlocked.Exchange(ref thrown, 1) == 0)
            {
                host.Store.Dispatch(new Increment());
                throw failure;
            }

            return value;
        });
        count.Value.ShouldBe(0);

        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();
        renderer.Pending.ShouldBe(1);
        host.Store.Dispatch(new Increment());

        renderer.Pending.ShouldBe(1);
        host.Renders.ShouldBe(1);
        await renderer.RunNextAsync();
        renderer.Pending.ShouldBe(0);
        host.Renders.ShouldBe(2);
        count.Value.ShouldBe(3);
        host.Logs().ShouldBe([(2002, LogLevel.Warning, Host.EvaluateFailedMessage, failure)]);
    }

    [Fact]
    public async Task Evaluate_ComparesWithLastRendererObservedValue()
    {
        // The change check compares with the value the renderer last read (or Evaluate last computed), under the
        // selection's comparer: an unchanged selection schedules a check but renders nothing.
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        using var core = host.Core(renderer.InvokeAsync);
        using var neverChanges = core.Select(Count, EqualityComparer<int>.Create((_, _) => true, _ => 0));
        var bucket = 10;
        using var bucketed = core.Select(state => Count(state) / bucket);
        neverChanges.Value.ShouldBe(0);
        bucketed.Value.ShouldBe(0);

        // Count 1: equal under its comparer, and still bucket 0.
        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();
        host.Renders.ShouldBe(0);

        // A parameter change the renderer observed (bucket 1 reads 1): the next commit leaves it unchanged.
        bucket = 1;
        bucketed.Value.ShouldBe(1);
        host.Store.Dispatch(new Probe());
        await renderer.RunNextAsync();
        host.Renders.ShouldBe(0);

        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();
        host.Renders.ShouldBe(1);

        // No render read the value: Evaluate's own computation (2) is now the baseline.
        host.Store.Dispatch(new Probe());
        await renderer.RunNextAsync();
        host.Renders.ShouldBe(1);
        host.Logs().ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_ManyChangedSelections_OneStateHasChanged()
    {
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        using var core = host.Core(renderer.InvokeAsync);
        using var first = core.Select(Count);
        using var second = core.Select(state => Count(state) * 2);
        using var third = core.Select(state => -Count(state));
        using var fourth = core.Select(state => Count(state) + 1);

        host.Store.Dispatch(new Increment());
        host.Store.Dispatch(new Increment());
        renderer.Pending.ShouldBe(1);
        await renderer.RunNextAsync();

        host.Renders.ShouldBe(1);
        renderer.Pending.ShouldBe(0);
    }

    [Fact]
    public async Task Evaluate_DisposedSelection_NotChecked()
    {
        // A disposed selection leaves the change check; the core's store subscription stays for the others.
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        using var core = host.Core(renderer.InvokeAsync);
        var evaluated = 0;
        var disposedOne = core.Select(state =>
        {
            evaluated++;
            return Count(state);
        });
        disposedOne.Value.ShouldBe(0);
        disposedOne.Dispose();
        disposedOne.Dispose();

        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();

        evaluated.ShouldBe(1);
        host.Renders.ShouldBe(0);
    }

    [Fact]
    public async Task Evaluate_AfterDispose_NoStateHasChanged()
    {
        // An Evaluate already pending when the component is disposed resets the flag and returns; after Dispose the store
        // subscription is gone, so a commit schedules nothing.
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        var core = host.Core(renderer.InvokeAsync);
        using var count = core.Select(Count);
        count.Value.ShouldBe(0);

        host.Store.Dispatch(new Increment());
        core.Dispose();
        core.Dispose();
        await renderer.RunNextAsync();
        host.Store.Dispatch(new Increment());

        renderer.Pending.ShouldBe(0);
        host.Renders.ShouldBe(0);
    }

    [Fact]
    public async Task Evaluate_StateHasChangedThrows_LoggedAndNextCommitSchedules()
    {
        // Evaluate catches its own exceptions: the task completes, and the flag it reset lets the next commit schedule. The
        // component did not re-render, so the log is its own event (2003), not the change-check failure (2002).
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        var failure = new InvalidOperationException("render");
        var renders = 0;
        using var core = host.Core(renderer.InvokeAsync, () =>
        {
            if (++renders == 1)
            {
                throw failure;
            }
        });
        using var count = core.Select(Count);

        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();
        host.Store.Dispatch(new Increment());
        await renderer.RunNextAsync();

        renders.ShouldBe(2);
        renderer.Faulted.ShouldBe(0);
        host.Logs().ShouldBe([(2003, LogLevel.Warning, Host.RenderRequestFailedMessage, failure)]);
    }

    [Fact]
    public async Task Select_OneStoreSubscription_FirstSelectSubscribes()
    {
        // No selection, no subscription: a commit schedules nothing. The first Select subscribes once for all
        // selections, so Dispose removes the only one.
        await using var host = await Host.CreateAsync();
        var renderer = new QueuedRenderer();
        var core = host.Core(renderer.InvokeAsync);
        host.Core(renderer.InvokeAsync).Dispose();

        host.Store.Dispatch(new Increment());
        renderer.Pending.ShouldBe(0);

        using var first = core.Select(Count);
        using var second = core.Select(Count);
        host.Store.Dispatch(new Increment());
        renderer.Pending.ShouldBe(1);
        await renderer.RunNextAsync();
        core.Dispose();
        host.Store.Dispatch(new Increment());
        renderer.Pending.ShouldBe(0);
    }

    private static int Count(StateSnapshot state) => state.Get<Counter>().Value;

    // A renderer that queues every work item until the test runs it, the posting shape of InvokeAsync.
    private sealed class QueuedRenderer
    {
        private readonly ConcurrentQueue<Func<Task>> _queue = new();
        private int _faulted;

        public int Pending => _queue.Count;

        public int Faulted => _faulted;

        public Task InvokeAsync(Func<Task> work)
        {
            var completion = new TaskCompletionSource();
            _queue.Enqueue(async () =>
            {
                try
                {
                    await work();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _faulted);
                    completion.SetException(ex);
                }
            });
            return completion.Task;
        }

        public Task RunNextAsync()
        {
            _queue.TryDequeue(out var work).ShouldBeTrue();
            return work.Invoke().WaitAsync(_bound, TestContext.Current.CancellationToken);
        }
    }

    private sealed class Host : IAsyncDisposable
    {
        public const string ScheduleFailedMessage = "Scheduling a component's change check on its renderer failed; the next commit schedules it again";
        public const string EvaluateFailedMessage = "A component's change check failed; the component re-renders so that its render surfaces the error";
        public const string RenderRequestFailedMessage = "Requesting a component's re-render after a change failed; the next commit checks it again";

        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly FakeLogCollector _collector = new();
        private int _renders;

        private Host(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            Store = _scope.ServiceProvider.GetRequiredService<IStore>();
            Journal = provider.GetRequiredService<CausalJournal>();
        }

        public IStore Store { get; }

        public CausalJournal Journal { get; }

        public TaskCompletionSource Rendered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Renders => Volatile.Read(ref _renders);

        public static async Task<Host> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<CausalJournal>();
            services.AddDucky(d => d.AddSlice<CounterSlice>().AddSlice<ProbeSlice>().Use<CausalRecorder>());
            var host = new Host(services.BuildServiceProvider());
            await host.Store.InitializeAsync(TestContext.Current.CancellationToken);
            return host;
        }

        public SubscriptionCore Core(Func<Func<Task>, Task> invokeAsync, Action? stateHasChanged = null) =>
            new(Store, invokeAsync, () =>
            {
                stateHasChanged?.Invoke();
                Interlocked.Increment(ref _renders);
                Rendered.TrySetResult();
            }, new FakeLogger(_collector));

        public IEnumerable<(int Id, LogLevel Level, string Message, Exception? Exception)> Logs() =>
            _collector.GetSnapshot().Select(record => (record.Id.Id, record.Level, record.Message, record.Exception));

        // Depth 0 and its own correlation: no drainer scope flowed into the render that dispatched it.
        public void ShouldStartOwnChain<TAction>()
        {
            var context = Journal.Seen.Where(c => c.Action is TAction).ShouldHaveSingleItem();
            context.Depth.ShouldBe(0);
            context.CorrelationId.ShouldBe(context.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}

internal sealed record Probe;

internal sealed record Probed(int Count);

internal sealed class ProbeSlice : Slice<Probed>
{
    public ProbeSlice() => On<Probe>(state => state with { Count = state.Count + 1 });

    protected override Probed Initial => new(0);
}

internal sealed class CausalJournal
{
    public ConcurrentQueue<ActionContext> Seen { get; } = new();
}

internal sealed class CausalRecorder(CausalJournal journal) : Middleware
{
    public override void BeforeReduce(ActionContext context) => journal.Seen.Enqueue(context);
}
