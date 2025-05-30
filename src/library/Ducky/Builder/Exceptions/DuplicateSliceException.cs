namespace Ducky.Builder;

/// <summary>
/// Exception thrown when a slice is registered multiple times.
/// </summary>
public class DuplicateSliceException : InvalidOperationException
{
    /// <summary>
    /// Gets the type of slice that was duplicated.
    /// </summary>
    public Type SliceType { get; }

    /// <summary>
    /// Initializes a new instance of the DuplicateSliceException class.
    /// </summary>
    /// <param name="sliceType">The type of slice that was duplicated.</param>
    public DuplicateSliceException(Type sliceType)
        : base(CreateMessage(sliceType))
    {
        SliceType = sliceType;
    }

    private static string CreateMessage(Type sliceType)
    {
        return $@"The slice for state type '{sliceType.Name}' has already been registered.

Each state type can only have one slice registered.

To fix this:
1. Remove duplicate calls to AddSlice<{sliceType.Name}>()
2. If you need multiple reducers for the same state, combine them in a single slice

Example of combining reducers:
public class Combined{sliceType.Name}Slice : SliceReducers<{sliceType.Name}>
{{
    public Combined{sliceType.Name}Slice()
    {{
        On<Action1>((state, action) => /* handle Action1 */);
        On<Action2>((state, action) => /* handle Action2 */);
    }}
}}";
    }

    /// <summary>
    /// Initializes a new instance of the DuplicateSliceException class.
    /// </summary>
    public DuplicateSliceException()
        : base()
    {
        SliceType = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance of the DuplicateSliceException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DuplicateSliceException(string message)
        : base(message)
    {
        SliceType = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance of the DuplicateSliceException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DuplicateSliceException(string message, Exception innerException)
        : base(message, innerException)
    {
        SliceType = typeof(object);
    }
}
