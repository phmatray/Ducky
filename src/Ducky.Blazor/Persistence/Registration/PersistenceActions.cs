namespace Ducky.Blazor;

/// <summary>Dispatched once when a hydration attempt has read storage.</summary>
/// <param name="StateRestored">Whether any slice was restored.</param>
public sealed record HydrationCompleted(bool StateRestored)
{
    /// <summary>Gets the scope epoch of the attempt (SPEC §11.5).</summary>
    internal int ScopeEpoch { get; init; }
}

/// <summary>Dispatched once when a hydration attempt failed or timed out; persistence resumes.</summary>
/// <param name="ErrorType">The type name of the error.</param>
/// <param name="Message">The error message.</param>
public sealed record HydrationFailed(string ErrorType, string Message)
{
    /// <summary>Gets the scope epoch of the attempt (SPEC §11.5).</summary>
    internal int ScopeEpoch { get; init; }
}

/// <summary>
/// Removes the stored state of each persisted slice, under its exact key. Storage only: the store's state is unchanged.
/// </summary>
/// <remarks>
/// A change signalled before the clear is not written back; the next change is written even if it equals what was stored.
/// An app that also wants its state reset handles this action (or its own, such as a sign-out) in its slices.
/// </remarks>
public sealed record ClearPersistedState;
