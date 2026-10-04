namespace Ducky.Draft;

/// <summary>A copy-on-write draft of an immutable <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The drafted record type.</typeparam>
public interface IDraft<out T> : IRevocable
    where T : class
{
    /// <summary>Gets a value indicating whether the draft may differ from its original.</summary>
    bool IsDirty { get; }

    /// <summary>Builds the result.</summary>
    /// <returns>The original instance when nothing changed, else a new instance.</returns>
    T Build();
}
