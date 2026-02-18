namespace Ducky.Blazor;

/// <summary>
/// Provides asynchronous lazy initialization for a resource, delaying its creation until it is needed.
/// </summary>
/// <remarks>
/// This is a simple implementation for async lazy initialization.
/// For a more advanced version, consider using DotNext.Threading via NuGet.
/// </remarks>
/// <typeparam name="T">The type of the resource to be initialized asynchronously.</typeparam>
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _lazy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with a synchronous value factory.
    /// </summary>
    /// <param name="valueFactory">The function used to produce the value when it is needed.</param>
    public AsyncLazy(Func<T> valueFactory)
    {
        _lazy = new Lazy<Task<T>>(() => Task.FromResult(valueFactory()));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with an asynchronous task factory.
    /// </summary>
    /// <param name="taskFactory">The asynchronous function used to produce the value when it is needed.</param>
    public AsyncLazy(Func<Task<T>> taskFactory)
    {
        _lazy = new Lazy<Task<T>>(taskFactory);
    }

    /// <summary>
    /// Gets the lazily initialized value as a Task.
    /// </summary>
    public Task<T> Value => _lazy.Value;

    /// <summary>
    /// Gets a value indicating whether the value has been created.
    /// </summary>
    public bool IsValueCreated => _lazy.IsValueCreated;
}
