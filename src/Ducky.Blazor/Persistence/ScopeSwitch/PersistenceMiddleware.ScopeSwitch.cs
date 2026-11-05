using System.Collections.Immutable;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

// SPEC §11.6 "Scope switch": a user change makes a new attempt current, resets the scoped slices and reads the new scope.
internal sealed partial class PersistenceMiddleware
{
    private readonly CancellationTokenSource _stopped = new(); // every switch attempt's token is linked to it: ended by dispose phase 5a
    private ImmutableDictionary<int, Attempt> _attempts = ImmutableDictionary<int, Attempt>.Empty;
    private Persisted[] _readable = [];
    private string[] _resets = [];
    private AuthenticationStateProvider? _auth;
    private int _scopeVersion;
    private bool _switchesStopped; // under _issue: set at dispose phase 5a, before _stopped can be disposed

    // Under _issue, before the epoch's Hydrating restore: the writers' skip reads its key set lock-free. One entry per switch,
    // like the epoch-to-scope map: a queued snapshot of any older epoch can still ask.
    private void RecordAttempt(Attempt attempt) => Volatile.Write(ref _attempts, _attempts.SetItem(attempt.Epoch, attempt));

    // Before the epoch-0 resolution starts, so a change racing it becomes a switch that supersedes attempt 0: in the
    // synchronous prefix on the server, inside the hand-off callback in the browser (the renderer's provider, §11.6).
    private void SubscribeToUserChanges(Persisted[] slices)
    {
        if (!slices.Any(p => Scoped(p.Options)))
        {
            return;
        }

        if (_registration.Options.IsBrowser)
        {
            _persistence.Gate.OnFirstRegistration(() => Subscribe(_persistence.Gate.Services!));
        }
        else
        {
            Subscribe(_services);
        }
    }

    // GetService: an explicit Scope needs no provider, and without one there is no subscription. Never throws: in the browser
    // this runs inside a component's Select (§6.10).
    private void Subscribe(IServiceProvider services)
    {
        try
        {
            _auth = services.GetService<AuthenticationStateProvider>();
            _auth?.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }
#pragma warning disable CA1031 // justification: a provider factory's failure must not reach the registering component
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Log.AuthSubscribeFailed(_log, exception);
        }
    }

    // Dispose phase 5a: no new switch, and the attempts' scope waits and reads end. CancelAsync: the scope delegate can
    // register on these tokens (§6.3), so the task can fault; the caller ignores that and flushes.
    private Task StopSwitchesAsync()
    {
        lock (_issue)
        {
            _switchesStopped = true;
        }

        _auth?.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        return _stopped.CancelAsync();
    }

    // Never throws into the circuit: everything runs inside an async method, and the attempt turns a failure into its
    // HydrationFailed. Runs on the notifying thread up to the first await.
    private void OnAuthenticationStateChanged(Task<AuthenticationState> state) => _ = SwitchAsync();

    // §11.6: synchronously under _issue a new epoch from the counter, a new current attempt and one reset restore. The old
    // scope's last state is handed over from the drainer when that reset commits (HandOverFinalSnapshots), never captured
    // here: it could be older than commits queued ahead of the reset. Then the attempt runs like any other (§11.5 steps 4-7).
    private async Task SwitchAsync()
    {
        var attempt = BeginSwitch();
        if (attempt is null)
        {
            return;
        }

        Persisted[] slices = [.. _readable.Where(p => attempt.Keys.Contains(p.Slice.Key))];
        await HydrateAsync(slices, attempt, attempt.Cancellation!.Token).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // The synchronous part, or null once the switches are stopped: a notification already in flight when dispose unsubscribed,
    // or one a browser hand-off after dispose subscribed (its callback is never withdrawn), takes no epoch and never touches
    // _stopped, which dispose disposes after the flush.
    private Attempt? BeginSwitch()
    {
        lock (_issue)
        {
            if (_switchesStopped)
            {
                return null;
            }

            var attempt = new Attempt { Cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopped.Token) };
            var superseded = Current!;
            var scoped = _readable.Where(p => Scoped(p.Options)).Select(static p => p.Slice.Key).ToImmutableHashSet();
            attempt.Epoch = ++_scopeVersion;
            attempt.Keys = superseded.Restored ? scoped : scoped.Union(superseded.Keys);
            Current = attempt;
            RecordAttempt(attempt);

            // CancelAsync: requested at once, while a callback a Scope delegate registered runs away from this lock and can't
            // throw here. Its read frees the key's loop for the new attempt's (INV-14).
            _ = superseded.Cancellation?.CancelAsync();
            var reset = Status(new(PersistenceStatus.Hydrating) { ScopeEpoch = attempt.Epoch });
            foreach (var key in _resets)
            {
                reset[key] = Store.InitialState.Get(key);
            }

            Store.Restore(reset, Origin.Hydration);
            return attempt;
        }
    }

    // On the drainer: the reset is the Hydration restore that changes the epoch, and its PreviousState the newest old-epoch
    // state. Only then may the epoch's reads run: a reset queued behind a drain would otherwise let a read overtake the final
    // snapshot it must see (§11.5 "Switch reads"). A restore of another origin triggers no write (INV-16), and an epoch no
    // switch made (the public IStore.Restore can name one) has no attempt to tell.
    private void HandOverFinalSnapshots(ActionContext context)
    {
        var epoch = context.State.Get<PersistenceState>().ScopeEpoch;
        if (context.Origin == Origin.Hydration && epoch != context.PreviousState.Get<PersistenceState>().ScopeEpoch)
        {
            foreach (var writer in _writers.Values)
            {
                writer.EnqueueFinal(context.PreviousState);
            }

            Volatile.Read(ref _attempts).GetValueOrDefault(epoch)?.ResetCommitted.TrySetResult();
        }
    }
}
