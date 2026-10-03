using CsCheck;
using Ducky.Tests.EffectFixtures;
using Ducky.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §17.4 (the EffectPolicies_ModelBased property), §6.6; INV-11. Random streams of dispatches over 4 policies x 3 keys
// (no key, A, B), interleaved with completions of a random run inside its handler, against a reference model of §6.6.
// Each handler registers its gate continuation (ExecuteSynchronously) before it signals its start, so releasing a gate
// runs the rest of that handler, its Loaded dispatch included, inline on the test thread. Only a Queue successor starts on
// a pool thread, and every step waits for the starts the model predicts before it compares.
public sealed class EffectPolicyModelTests
{
    private const int Policies = 4;
    private const int Keys = 3;

    [Fact]
    public void EffectPolicies_ModelBased()
    {
        // A step (k, c) with k > 0 dispatches policy c % 4 with key c / 4; (0, c) releases the (c % n)-th of the n runs
        // inside their handler, oldest first, or does nothing when n is 0. Three dispatches per completion over 32 to 64
        // steps make runs of one (policy, key) collide often; each of the five original committed seeds reaches Switch
        // supersession, a Queue successor start and an Exhaust drop. A seed appended later (a shrunk failure) may not, so
        // the gated run asserts these paths over all committed seeds together.
        var steps = Gen.Select(Gen.Int[0, 3], Gen.Int[0, (Policies * Keys) - 1]).List[32, 64];
        var reached = new int[3];
        Property.Check(steps, run =>
        {
            using var store = new DuckyStore(
                [new SeenSlice()],
                NullLogger.Instance,
                effects: () =>
                [
                    (new ModelEffect<MergeFire>(Concurrency.Merge), false),
                    (new ModelEffect<SwitchFire>(Concurrency.Switch), false),
                    (new ModelEffect<ExhaustFire>(Concurrency.Exhaust), false),
                    (new ModelEffect<QueueFire>(Concurrency.Queue), false),
                ]);
            var scenario = new Scenario(store);
            scenario.Play(run);
            Interlocked.Add(ref reached[0], scenario.Supersessions);
            Interlocked.Add(ref reached[1], scenario.SuccessorStarts);
            Interlocked.Add(ref reached[2], scenario.Drops);
        });
        if (Environment.GetEnvironmentVariable("DUCKY_PROPERTY_SEEDS") is not null)
        {
            reached.ShouldAllBe(count => count > 0, "the committed seeds miss Switch supersession, Queue chaining or Exhaust drops");
        }
    }

    internal enum RunState
    {
        Waiting,
        InHandler,
        Done,
        Dropped,
    }

    // One run as the model sees it, and the actual signals of its handler.
    internal sealed class Probe(int id, Lane lane)
    {
        public int Id { get; } = id;

        public Lane Lane { get; } = lane;

        public TaskCompletionSource Started { get; } = new();

        public TaskCompletionSource Gate { get; } = new();

        public RunState State { get; set; }

        public bool Superseded { get; set; }
    }

    // One (policy, key): the model's runs, and the actual handler concurrency and start order.
    internal sealed class Lane(Concurrency policy, string? key)
    {
        private int _active;
        private int _maxActive;

        public Concurrency Policy { get; } = policy;

        public string? Key { get; } = key;

        public List<Probe> Runs { get; } = [];

        public System.Collections.Concurrent.ConcurrentQueue<int> Starts { get; } = new();

        public int MaxActive => Volatile.Read(ref _maxActive);

        public void Enter(int id)
        {
            var now = Interlocked.Increment(ref _active);
            InterlockedMax(ref _maxActive, now);
            Starts.Enqueue(id);
        }

        public void Exit() => Interlocked.Decrement(ref _active);

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
            {
            }
        }
    }

    internal abstract record Fire(Probe Probe);

    internal sealed record MergeFire(Probe Probe) : Fire(Probe);

    internal sealed record SwitchFire(Probe Probe) : Fire(Probe);

    internal sealed record ExhaustFire(Probe Probe) : Fire(Probe);

    internal sealed record QueueFire(Probe Probe) : Fire(Probe);

    private sealed class ModelEffect<TFire>(Concurrency policy) : Effect<TFire>
        where TFire : Fire
    {
        public override Concurrency Policy => policy;

        protected override object? ConcurrencyKey(TFire action) => action.Probe.Lane.Key;

        public override Task Handle(TFire action, EffectContext context, CancellationToken cancellationToken)
        {
            var probe = action.Probe;
            var done = probe.Gate.Task.ContinueWith(
                _ =>
                {
                    probe.Lane.Exit();
                    context.Dispatch(new Loaded(probe.Id));
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            probe.Lane.Enter(probe.Id);
            probe.Started.SetResult();
            return done;
        }
    }

    private sealed class Scenario(DuckyStore store)
    {
        private readonly Dictionary<(Concurrency, string?), Lane> _lanes = [];
        private readonly List<Probe> _probes = [];
        private readonly List<object> _expected = [];

        public int Supersessions { get; private set; }

        public int SuccessorStarts { get; private set; }

        public int Drops { get; private set; }

        public void Play(List<(int Kind, int Choice)> steps)
        {
            foreach (var (kind, choice) in steps)
            {
                if (kind > 0)
                {
                    Dispatch((Concurrency)(choice % Policies), (choice / Policies) switch { 0 => null, 1 => "A", _ => "B" });
                }
                else
                {
                    Complete(choice);
                }

                Compare();
            }

            // Quiescence: release every run until none is left inside its handler.
            while (_probes.Exists(p => p.State == RunState.InHandler))
            {
                Complete(0);
                Compare();
            }

            Await(store.WhenIdleAsync(TestContext.Current.CancellationToken));
            store.Dispatcher.SlotCount.ShouldBe(0);
            foreach (var lane in _lanes.Values)
            {
                if (lane.Policy is Concurrency.Queue or Concurrency.Exhaust)
                {
                    lane.MaxActive.ShouldBeLessThanOrEqualTo(1, $"{lane.Policy} {lane.Key} overlapped");
                }

                if (lane.Policy == Concurrency.Queue)
                {
                    lane.Starts.ShouldBe(lane.Runs.Select(p => p.Id), $"Queue {lane.Key} ran out of dispatch order");
                }
            }
        }

        private void Dispatch(Concurrency policy, string? key)
        {
            if (!_lanes.TryGetValue((policy, key), out var lane))
            {
                _lanes[(policy, key)] = lane = new Lane(policy, key);
            }

            var probe = new Probe(_probes.Count, lane);
            var busy = lane.Runs.Where(p => p.State is RunState.InHandler or RunState.Waiting).ToList();
            probe.State = (policy, busy.Count) switch
            {
                (Concurrency.Exhaust, > 0) => RunState.Dropped,
                (Concurrency.Queue, > 0) => RunState.Waiting,
                _ => RunState.InHandler,
            };
            if (policy == Concurrency.Switch && busy.Count > 0)
            {
                Supersessions++;
                busy.ForEach(p => p.Superseded = true);
            }

            if (probe.State == RunState.Dropped)
            {
                Drops++;
            }

            _probes.Add(probe);
            if (probe.State != RunState.Dropped)
            {
                lane.Runs.Add(probe);
            }

            store.Dispatch(policy switch
            {
                Concurrency.Merge => new MergeFire(probe),
                Concurrency.Switch => new SwitchFire(probe),
                Concurrency.Exhaust => new ExhaustFire(probe),
                _ => (Fire)new QueueFire(probe),
            });
        }

        // Only the latest Switch run of a key dispatches; a Queue completion lets the oldest waiting run of its key start.
        private void Complete(int choice)
        {
            var inHandler = _probes.Where(p => p.State == RunState.InHandler).ToList();
            if (inHandler.Count == 0)
            {
                return;
            }

            var probe = inHandler[choice % inHandler.Count];
            probe.State = RunState.Done;
            if (!probe.Superseded)
            {
                _expected.Add(new Loaded(probe.Id));
            }

            probe.Gate.SetResult();
            if (probe.Lane.Runs.Find(p => p.State == RunState.Waiting) is { } next)
            {
                SuccessorStarts++;
                next.State = RunState.InHandler;
            }
        }

        private void Compare()
        {
            foreach (var probe in _probes.Where(p => p.State == RunState.InHandler))
            {
                Await(probe.Started.Task);
            }

            _probes.Where(p => p.Started.Task.IsCompleted).Select(p => p.Id)
                .ShouldBe(_probes.Where(p => p.State is RunState.InHandler or RunState.Done).Select(p => p.Id));
            store.State.Get<Seen>().Actions.ShouldBe(_expected);

            // At least the model's slots: a Queue successor's handler starts on a pool thread, and when its gate is released
            // before that handler returns, the rest of its run, slot removal included, ends on that thread, after the test
            // thread's SetResult returned. Installs all happen on the test thread, so a run removing a newer run's slot (the
            // compare-and-remove rule broken) still shows here; Play checks equality at quiescence.
            store.Dispatcher.SlotCount.ShouldBeGreaterThanOrEqualTo(_lanes.Values.Sum(SlotsOf));
        }

        // A Switch or Queue key holds the slot of its latest run until that run ends (an older run's completion never
        // removes it); an Exhaust key holds one while its run is inside the handler; Merge has no slot.
        private static int SlotsOf(Lane lane) => lane.Policy switch
        {
            Concurrency.Merge => 0,
            Concurrency.Exhaust => lane.Runs.Exists(p => p.State == RunState.InHandler) ? 1 : 0,
            _ => lane.Runs[^1].State == RunState.Done ? 0 : 1,
        };

        private static void Await(Task task) =>
            task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }
}
