namespace TheBlazorState.Abstractions;

/// <summary>
/// Metadata companion for a [Persist] or [Shared] property.
/// Generated code creates one instance per annotated property.
/// </summary>
public sealed class StateMeta
{
    private const int MaxChangeLogSize = 10;
    private readonly TimeSpan? _ttl;
    private readonly List<StateChangeEntry> _changeLog = [];

    public StateMeta(TimeSpan? ttl)
    {
        _ttl = ttl;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>True if the value was restored from persistence.</summary>
    public bool WasRestored { get; private set; }

    /// <summary>True if the value has been modified since initialization.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>True if the value has exceeded its configured TTL.</summary>
    public bool IsStale =>
        _ttl.HasValue && DateTimeOffset.UtcNow - LastUpdated > _ttl.Value;

    /// <summary>The configured time-to-live, or null if none.</summary>
    public TimeSpan? TimeToLive => _ttl;

    /// <summary>UTC timestamp of last value change or restore.</summary>
    public DateTimeOffset LastUpdated { get; private set; }

    /// <summary>
    /// Recent change history (newest first), capped at 10 entries.
    /// Written by the library on each value change — no manual logging needed.
    /// </summary>
    public IReadOnlyList<StateChangeEntry> ChangeLog => _changeLog;

    /// <summary>
    /// Records a value transition in the change log.
    /// </summary>
    public void LogChange(string? oldValue, string? newValue, ChangeSource source = ChangeSource.Local)
    {
        _changeLog.Insert(0, new StateChangeEntry(DateTimeOffset.UtcNow, oldValue, newValue, source));
        if (_changeLog.Count > MaxChangeLogSize)
            _changeLog.RemoveRange(MaxChangeLogSize, _changeLog.Count - MaxChangeLogSize);
    }

    /// <summary>
    /// When true, the next <see cref="RaiseChanged"/> call should NOT trigger
    /// persistence side-effects (e.g. writing back to browser storage).
    /// Used by cross-tab sync to prevent infinite feedback loops.
    /// Reset automatically after <see cref="RaiseChanged"/> fires.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public bool SuppressPersist { get; set; }

    /// <summary>Fires whenever the property value changes.</summary>
    public event Action? OnChanged;

    /// <summary>
    /// Fires after <see cref="OnChanged"/> completes, allowing handlers to
    /// react to state that was updated by OnChanged handlers.
    /// Used by the generated code to schedule re-renders after all side-effects.
    /// </summary>
    public event Action? OnAfterChanged;

    /// <summary>Called by generated code to record that a value was restored from persistence.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void MarkRestored(DateTimeOffset persistedAt)
    {
        WasRestored = true;
        LastUpdated = persistedAt;
    }

    /// <summary>Called by generated code when the property value changes.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void MarkDirty()
    {
        IsDirty = true;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>Called by generated code to raise the OnChanged event.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void RaiseChanged()
    {
        OnChanged?.Invoke();
        OnAfterChanged?.Invoke();
        SuppressPersist = false;
    }

    /// <summary>Called by generated code during Dispose to detach all handlers.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public void ClearHandlers()
    {
        OnChanged = null;
        OnAfterChanged = null;
    }
}

/// <summary>
/// A single entry in a property's change log.
/// </summary>
public sealed record StateChangeEntry(
    DateTimeOffset Timestamp,
    string? OldValue,
    string? NewValue,
    ChangeSource Source);

/// <summary>
/// Identifies where a state change originated.
/// </summary>
public enum ChangeSource
{
    /// <summary>Changed locally (user interaction in this tab).</summary>
    Local,
    /// <summary>Changed by another tab via cross-tab sync.</summary>
    CrossTab
}
