namespace Ducky.Blazor;

// SPEC §11.6 "Re-assertion check": every Blazor Server circuit reconnect re-notifies the same user. Absorbed when the scope
// resolves synchronously to the one recorded for the current epoch and that epoch's attempt has issued its terminal: no
// epoch, no reset, no reads. Every other case runs the switch unchanged, reusing nothing from the check.
internal sealed partial class PersistenceMiddleware
{
    // Outside _issue, before any await: the scope delegate once, on the store-lifetime token. Only a ValueTask that completed
    // successfully is a result; an incomplete or faulted one is observed and discarded, and a throw (a disposed token
    // included) is none. In the browser the handler is subscribed inside the hand-off, so the renderer's services are there.
    private ScopeCheck CheckScope()
    {
        try
        {
            var services = _registration.Options.IsBrowser ? _persistence.Gate.Services! : _services;
            var pending = (_registration.Options.Scope ?? DuckyScopes.NameIdentifier)(services, _stopped.Token);
            if (pending.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD002 // justification: read only once IsCompletedSuccessfully, so nothing waits
                return new(true, Normalized(pending.Result));
#pragma warning restore VSTHRD002
            }

            // Stryker disable once Statement : observing it only keeps a later fault from being an unobserved task exception, which .NET ignores
            _ = DiscardAsync(pending.AsTask());
        }
#pragma warning disable CA1031 // justification: a failing delegate is no result of the check; the switch meets it again and ends in HydrationFailed
        catch (Exception)
#pragma warning restore CA1031
        {
            // No result: the switch runs.
        }

        return new(false, null);
    }

    // Under _issue: the current epoch's scope is known (null included) and ordinally equal, and its attempt issued its terminal.
    private bool IsReassertion(ScopeCheck check)
    {
        // Assigned up front, not an out variable: no mutant of the condition can leave it unassigned (Stryker Safe Mode, §17.8).
        var current = Current!;
        string? recorded = null;
        return check.Resolved
            && current.Terminal == 2
            && Scopes.TryGetValue(current.Epoch, out recorded)
            && string.Equals(recorded, check.Scope, StringComparison.Ordinal);
    }

    // An empty or whitespace scope would name the shared key {prefix}::{sliceKey}: it is null, never a fallback (INV-17).
    private static string? Normalized(string? scope) => string.IsNullOrWhiteSpace(scope) ? null : scope;

    // Observed, so a later fault is never an unobserved task exception; its result is not used.
#pragma warning disable VSTHRD003 // justification: the scope delegate's abandoned task, awaited only to observe it, never for its result
    private static async Task DiscardAsync(Task pending) => await pending.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#pragma warning restore VSTHRD003

    private readonly record struct ScopeCheck(bool Resolved, string? Scope);
}
