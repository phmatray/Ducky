using System.Text.Json;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// SPEC §11.8 history and inbound (INV-28). The history is an immutable array of (devToolsIndex, snapshot), capped at
// MaxAge, that only the drainer replaces (Volatile.Write); OnMessage, on the circuit's sync context, reads it once, so a
// jump racing a send restores the very entry its index names. The time-travel flag OnMessage sets is volatile, since the
// drainer reads it in MayDispatch.
internal sealed partial class DevToolsMiddleware
{
    private const string LibraryPrefix = "@ducky/";
    private HistoryEntry[] _history = []; // never written after it is published
    private volatile bool _timeTravel;

    // Library origins are never offered to MayDispatch (§6.4): only Local and Effect, both vetoed off the latest entry. An
    // inert DevTools can deliver no jump back to the latest, so the veto ends with the connection.
    public override bool MayDispatch(ActionContext context) => !_timeTravel || Volatile.Read(ref _connected) == 0;

    /// <summary>
    /// One inbound Redux DevTools message, forwarded by ducky.js (§11.9). A jump arrives stripped of the extension's state;
    /// a message above the inline budget arrives as <paramref name="tooLarge"/> without <paramref name="json"/>.
    /// </summary>
    [JSInvokable]
    public void OnMessage(string? json, long seq, bool tooLarge)
    {
        var command = Parse(json);
        // Inert (a connect .NET gave up on, a failed send): ducky.js may still forward messages, but the history may no
        // longer match the extension's index, so none touches the state. Connected with @@INIT pending (2) still handles them.
        if (Volatile.Read(ref _connected) == 0)
        {
            Log.DevToolsMessageIgnored(_log, seq, command?.Type, tooLarge);
            return;
        }

        switch (command?.Type)
        {
            case "JUMP_TO_STATE" or "JUMP_TO_ACTION" when command.ActionId is { } actionId:
                Jump(actionId, byPosition: false);
                break;
            case "JUMP_TO_STATE" or "JUMP_TO_ACTION" when command.Index is { } index:
                Jump(index, byPosition: true);
                break;
            case "RESET": // the initial state, live again
                _timeTravel = false;
                Restore(Store.InitialState);
                break;
            case "IMPORT_STATE" when command.NextLiftedState?.ComputedStates is [.., { State: { } state }]:
                // The last computed state, each slice deserialized by Restore with its declared StateType (§5.2); an
                // unknown key restores nothing, and a payload that fails to deserialize is skipped with a Warning. The
                // imported state is the extension's latest.
                _timeTravel = false;
                Store.Restore(state.Where(static entry => !IsLibrary(entry.Key)).ToDictionary(static entry => entry.Key, static entry => (object)entry.Value), Origin.DevTools);
                break;
            default: // COMMIT, ROLLBACK, SWEEP, TOGGLE_ACTION, any other message, and (until pulled) a large one
                Log.DevToolsMessageIgnored(_log, seq, command?.Type, tooLarge);
                break;
        }
    }

    // Only DISPATCH messages carry the extension's commands; a message that isn't of the wire shape is no command. One
    // return, after the catch, so that no mutant can break definite assignment (§17.8, no Safe Mode).
    private static DevToolsCommand? Parse(string? json)
    {
        DevToolsCommand? command = null;
        try
        {
            if (json is not null && JsonSerializer.Deserialize(json, BlazorWireContext.Default.DevToolsMessage) is { Type: "DISPATCH" } message)
            {
                command = message.Payload;
            }
        }
        catch (JsonException)
        {
            // not JSON, or not of the wire shape: no command
        }

        return command;
    }

    // Off the latest entry time travel engages; on it, it releases. A target the history no longer holds is ignored. The
    // extension's index is a position in its lifted state, its actionId the devToolsIndex. Past MaxAge it keeps slot 0
    // (actionId 0) as the committed state, then the latest MaxAge-1 actions: a full history lines up with it slot for slot,
    // and before that positions are ids.
    private void Jump(long target, bool byPosition)
    {
        var history = Volatile.Read(ref _history);
        var position = byPosition || (target == 0 && history.Length == _options.MaxAge) ? target : Array.FindIndex(history, entry => entry.Index == target);
        if (position >= 0 && position < history.Length)
        {
            _timeTravel = position != history.Length - 1;
            Restore(history[position].State);
            return;
        }

        Log.DevToolsIndexEvicted(_log, target);
    }

    // The typed snapshot's own instances (reference-equal slices), except the library slices.
    private void Restore(StateSnapshot snapshot) =>
        Store.Restore(Store.Slices.Where(static slice => !IsLibrary(slice.Key)).ToDictionary(static slice => slice.Key, slice => snapshot.Get(slice.Key)), Origin.DevTools);

    // Drainer only: the oldest entries are dropped so the new array holds at most MaxAge, and always the latest.
    private void Record(long index, StateSnapshot state)
    {
        var history = _history;
        var kept = Math.Min(history.Length, Math.Max(_options.MaxAge - 1, 0));
        Volatile.Write(ref _history, [.. history.AsSpan(history.Length - kept), new(index, state)]);
    }

    private static bool IsLibrary(string key) => key.StartsWith(LibraryPrefix, StringComparison.Ordinal);

    private readonly record struct HistoryEntry(long Index, StateSnapshot State);
}
