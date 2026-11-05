namespace Ducky.Blazor;

// SPEC §11.5 step 7, §11.6 step 1: every attempt has exactly one live deadline. During the init phase it is the init-phase
// HydrationTimeout, which ends whichever attempt is current; once _initPhase is clear a switch arms its own.
internal sealed partial class PersistenceMiddleware
{
    // Under _issue, in the switch: decided by _initPhase, never by "InitializeAsync has returned", an asynchronous
    // continuation that can't be ordered against the switch. The switch disposes it once its attempt has run.
    private ITimer? ArmSwitchDeadline(Attempt attempt) => _initPhase
        ? null
        : _time.CreateTimer(_ => EndSwitchAttempt(attempt), null, _registration.Options.HydrationTimeout, Timeout.InfiniteTimeSpan);

    // The per-attempt deadline, under _issue as EndInitPhase: ends its attempt if that is still current and open, and cancels
    // its reads so a hung one frees the key's loop. A superseded or ended attempt's deadline is discarded here: Claim and
    // IssueTerminal ignore such an attempt, and its token is cancelled already, or no longer read. Once the switches are
    // stopped it issues nothing: the attempt of a scope delegate that ignores its token is still open and its timer still
    // armed after dispose, and _stopped has cancelled its reads.
    private void EndSwitchAttempt(Attempt attempt)
    {
        lock (_issue)
        {
            if (_switchesStopped)
            {
                return;
            }

            Claim(attempt, TimedOut(attempt.Epoch));
            IssueTerminal(attempt);
            _ = attempt.Cancellation!.CancelAsync();
        }
    }

    private HydrationFailed TimedOut(int epoch) =>
        new(nameof(TimeoutException), $"Hydration did not end within HydrationTimeout ({_registration.Options.HydrationTimeout}).") { ScopeEpoch = epoch };
}
