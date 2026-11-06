using System.Text.Json.Serialization;

namespace Ducky.Samples.Server.Client;

// Keys are explicit: the E2E specs read localStorage, sessionStorage and the cache under {KeyPrefix}:{scope?}:{key}.

/// <summary>Local storage, synced across tabs and across the circuit and WASM stores of one document.</summary>
public sealed record CounterState(int Value);

/// <summary>Adds one to the counter.</summary>
[ActionType("counter/increment")]
public sealed record Increment;

/// <summary>The cross-tab counter.</summary>
[Persist(SyncAcrossTabs = true)]
public sealed class CounterSlice : Slice<CounterState>
{
    /// <summary>Registers the reducers.</summary>
    public CounterSlice() => On<Increment>(state => state with { Value = state.Value + 1 });

    /// <inheritdoc />
    public override string Key => "counter";

    /// <inheritdoc />
    protected override CounterState Initial => new(0);
}

/// <summary>Session storage: per tab, never synced.</summary>
public sealed record DraftState(string Text);

/// <summary>The draft text changed (its payload is redacted for DevTools).</summary>
[ActionType("draft/changed")]
public sealed record DraftChanged(string Text);

/// <summary>The session draft.</summary>
[Persist(Storage = PersistStorage.Session)]
public sealed class DraftSlice : Slice<DraftState>
{
    /// <summary>Registers the reducers.</summary>
    public DraftSlice() => On<DraftChanged>((_, action) => new DraftState(action.Text));

    /// <inheritdoc />
    public override string Key => "draft";

    /// <inheritdoc />
    protected override DraftState Initial => new("");
}

/// <summary>Local storage without cross-tab sync.</summary>
public sealed record PreferencesState(bool Dark);

/// <summary>Flips the theme.</summary>
[ActionType("preferences/toggled")]
public sealed record ThemeToggled;

/// <summary>The unsynced Local slice.</summary>
[Persist]
public sealed class PreferencesSlice : Slice<PreferencesState>
{
    /// <summary>Registers the reducers.</summary>
    public PreferencesSlice() => On<ThemeToggled>(state => state with { Dark = !state.Dark });

    /// <inheritdoc />
    public override string Key => "preferences";

    /// <inheritdoc />
    protected override PreferencesState Initial => new(false);
}

/// <summary>Server storage, scoped by the signed-in user's NameIdentifier; inert on the WASM side.</summary>
public sealed record NotesState(IReadOnlyList<string> Items);

/// <summary>Appends a note.</summary>
[ActionType("notes/added")]
public sealed record NoteAdded(string Text);

/// <summary>The per-user notes in the server's IDistributedCache.</summary>
[Persist(Storage = PersistStorage.Server)]
public sealed class NotesSlice : Slice<NotesState>
{
    /// <summary>Registers the reducers.</summary>
    public NotesSlice() => On<NoteAdded>((state, action) => new NotesState([.. state.Items, action.Text]));

    /// <inheritdoc />
    public override string Key => "notes";

    /// <inheritdoc />
    protected override NotesState Initial => new([]);
}

/// <summary>A large or non-ASCII text: persisted, synced and prerendered, for the payload-size E2E specs.</summary>
public sealed record LargeState(string Kind, string Text);

/// <summary>Replaces the text with <paramref name="Chars"/> characters of <paramref name="Kind"/> (ascii, cjk or quote).</summary>
[ActionType("large/filled")]
public sealed record LargeFilled(string Kind, int Chars);

/// <summary>The large slice; the action carries only the size, so DevTools payloads stay small.</summary>
[Persist(SyncAcrossTabs = true)]
[Prerender]
public sealed class LargeSlice : Slice<LargeState>
{
    /// <summary>Registers the reducers.</summary>
    public LargeSlice() => On<LargeFilled>((_, action) => new LargeState(action.Kind, new string(Glyph(action.Kind), action.Chars)));

    /// <inheritdoc />
    public override string Key => "large";

    /// <inheritdoc />
    protected override LargeState Initial => new("ascii", "");

    // CJK is 3 UTF-8 bytes per character; a quote is escaped to two JSON characters.
    private static char Glyph(string kind) => kind switch
    {
        "cjk" => '語',
        "quote" => '"',
        _ => 'x',
    };
}

/// <summary>Counted by <see cref="DispatchingHandler"/>; not persisted.</summary>
public sealed record ApiCallsState(int Count);

/// <summary>An HttpClient request went out through the dispatching handler.</summary>
[ActionType("api/sent")]
public sealed record ApiCallSent(string Path);

/// <summary>The requests the handler saw.</summary>
public sealed class ApiCallsSlice : Slice<ApiCallsState>
{
    /// <summary>Registers the reducers.</summary>
    public ApiCallsSlice() => On<ApiCallSent>(state => state with { Count = state.Count + 1 });

    /// <inheritdoc />
    public override string Key => "api-calls";

    /// <inheritdoc />
    protected override ApiCallsState Initial => new(0);
}

/// <summary>Persistence, the prerender seed and DevTools serialize through this context (trim- and AOT-safe).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(DraftState))]
[JsonSerializable(typeof(PreferencesState))]
[JsonSerializable(typeof(NotesState))]
[JsonSerializable(typeof(LargeState))]
[JsonSerializable(typeof(ApiCallsState))]
[JsonSerializable(typeof(ProductsState))]
[JsonSerializable(typeof(Increment))]
[JsonSerializable(typeof(DraftChanged))]
[JsonSerializable(typeof(ThemeToggled))]
[JsonSerializable(typeof(NoteAdded))]
[JsonSerializable(typeof(LargeFilled))]
[JsonSerializable(typeof(ApiCallSent))]
[JsonSerializable(typeof(ProductsLoaded))]
[JsonSerializable(typeof(string[]))]
public sealed partial class SampleJson : JsonSerializerContext;
