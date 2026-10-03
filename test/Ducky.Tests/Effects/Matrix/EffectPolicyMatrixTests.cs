using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §17.4 (the full policy matrix), §6.6; INV-11. Every case dispatches Load(1) then Load(2) to one effect. Run 1 is
// keyed A (no key under Global); run 2 has no key (Global), key A (the same slot as run 1) or key B (its own slot), so
// Global and KeyA share a slot through the NullKey sentinel and a real key, and KeyB never interacts. Under PolicyCancel
// the gate of a run Switch supersedes is never released, so that run can only end through its own token's OCE, which
// Switch's CancelAsync raises on a pool thread; WhenIdleAsync completing proves the supersession cancelled it.
public sealed class EffectPolicyMatrixTests
{
    public enum Scope
    {
        Global,
        KeyA,
        KeyB,
    }

    public enum Outcome
    {
        // Each released run dispatches Loaded through its EffectContext, ignoring its token.
        Complete,

        // Each released run throws.
        Fault,

        // Each released run throws an OperationCanceledException on a token that isn't its own.
        ForeignOce,

        // Each run waits on its gate with its own token, so the policy's cancellation (Switch supersession) ends it.
        PolicyCancel,

        // Each run waits on its gate with its own token, and the store is disposed instead of releasing the gates.
        StoreDispose,
    }

    public static TheoryData<Concurrency, Scope, Outcome> Cases()
    {
        var cases = new TheoryData<Concurrency, Scope, Outcome>();
        foreach (var policy in Enum.GetValues<Concurrency>())
        {
            foreach (var scope in Enum.GetValues<Scope>())
            {
                foreach (var outcome in Enum.GetValues<Outcome>())
                {
                    cases.Add(policy, scope, outcome);
                }
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EffectPolicyMatrix(Concurrency policy, Scope scope, Outcome outcome)
    {
        var ct = TestContext.Current.CancellationToken;
        using var foreign = new CancellationTokenSource();
        await foreign.CancelAsync();
        var gates = new[] { new TaskCompletionSource(), new(), new() };
        var errors = new Exception[]
        {
            null!,
            Error(1, outcome, foreign.Token),
            Error(2, outcome, foreign.Token),
        };
        var invoked = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var effect = new PolicyHandler(policy, scope, async (load, context, token) =>
        {
            invoked.Enqueue(load.Id);
            if (outcome is Outcome.PolicyCancel or Outcome.StoreDispose)
            {
                await gates[load.Id].Task.WaitAsync(token).ConfigureAwait(false);
            }
            else
            {
                await gates[load.Id].Task.ConfigureAwait(false);
            }

            if (outcome is Outcome.Fault or Outcome.ForeignOce)
            {
                throw errors[load.Id];
            }

            context.Dispatch(new Loaded(load.Id));
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        if (outcome == Outcome.StoreDispose)
        {
            await store.DisposeAsync();
        }
        else
        {
            // Under PolicyCancel a superseded run keeps its gate closed, so only its own token's OCE can end it.
            foreach (var id in (int[])[1, 2])
            {
                if (!(outcome == Outcome.PolicyCancel && Superseded(id, policy, scope)))
                {
                    gates[id].SetResult();
                }
            }

            await store.WhenIdleAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
        }

        var (expectedInvoked, expectedEffects) = Expected(policy, scope, outcome, errors);
        invoked.Order().ShouldBe(expectedInvoked);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.OfType<EffectFailed>().Count().ShouldBe(expectedEffects.OfType<EffectFailed>().Count());
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), .. expectedEffects]);
    }

    // The reference rules of §6.6. Run 2 shares run 1's slot except under KeyB. Exhaust drops a sharing run 2; Queue
    // chains it, and a disposal while it waits means its handler never runs; Switch supersedes run 1, whose token is then
    // cancelled, so its dispatch is dropped and a foreign OCE counts as our cancellation, but a fault is still a fault.
    private static (int[] Invoked, object[] Effects) Expected(Concurrency policy, Scope scope, Outcome outcome, Exception[] errors)
    {
        var shared = scope != Scope.KeyB;
        var secondInvoked = !(shared && policy == Concurrency.Exhaust)
            && !(shared && policy == Concurrency.Queue && outcome == Outcome.StoreDispose);
        int[] invoked = secondInvoked ? [1, 2] : [1];
        var effects = invoked.SelectMany(id =>
        {
            var cancelled = outcome == Outcome.StoreDispose || Superseded(id, policy, scope);
            return outcome switch
            {
                Outcome.Fault => new object[] { Failed(errors[id]) },
                Outcome.ForeignOce when !cancelled => [Failed(errors[id])],
                Outcome.Complete or Outcome.PolicyCancel when !cancelled => [new Loaded(id)],
                _ => [],
            };
        });
        return (invoked, [.. effects]);
    }

    // Switch supersedes run 1 when run 2 shares its slot.
    private static bool Superseded(int id, Concurrency policy, Scope scope) =>
        id == 1 && scope != Scope.KeyB && policy == Concurrency.Switch;

    private static EffectFailed Failed(Exception error) =>
        new(typeof(PolicyHandler).ToString(), typeof(Load).ToString(), error);

    private static Exception Error(int id, Outcome outcome, CancellationToken foreign) => outcome == Outcome.ForeignOce
        ? new OperationCanceledException($"foreign {id}", foreign)
        : new InvalidOperationException($"fault {id}");

    private sealed class PolicyHandler(Concurrency policy, Scope scope, Func<Load, EffectContext, CancellationToken, Task> handle)
        : Effect<Load>
    {
        public override Concurrency Policy => policy;

        protected override object? ConcurrencyKey(Load action) => (scope, action.Id) switch
        {
            (Scope.Global, _) => null,
            (Scope.KeyB, 2) => "B",
            _ => "A",
        };

        public override Task Handle(Load action, EffectContext context, CancellationToken cancellationToken) =>
            handle(action, context, cancellationToken);
    }
}
