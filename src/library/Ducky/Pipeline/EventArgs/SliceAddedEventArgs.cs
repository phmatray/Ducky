namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when a slice is added to the store.
/// </summary>
public sealed class SliceAddedEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SliceAddedEventArgs"/> class.
    /// </summary>
    /// <param name="sliceKey">The key of the slice being added.</param>
    /// <param name="sliceType">The type of the slice being added.</param>
    public SliceAddedEventArgs(string sliceKey, Type sliceType)
    {
        SliceKey = sliceKey;
        SliceType = sliceType;
    }

    /// <summary>
    /// Gets the key of the slice being added.
    /// </summary>
    public string SliceKey { get; }

    /// <summary>
    /// Gets the type of the slice being added.
    /// </summary>
    public Type SliceType { get; }
}
