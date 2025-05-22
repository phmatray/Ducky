namespace Ducky.Blazor;

/// <summary>
/// Provides asynchronous lazy initialization for a resource, delaying its creation until it is needed.
/// </summary>
/// <remarks>
/// This is a simple implementation for async lazy initialization.
/// For a more advanced version, consider using DotNext.Threading via NuGet.
/// </remarks>
/// <typeparam name="T">The type of the resource to be initialized asynchronously.</typeparam>
public class AsyncLazy<T> : Lazy<Task<T>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with a synchronous value factory.
    /// </summary>
    /// <param name="valueFactory">The function used to produce the value when it is needed.</param>
    public AsyncLazy(Func<T> valueFactory)
        : base(() => Task.Factory.StartNew(valueFactory))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class with an asynchronous task factory.
    /// </summary>
    /// <param name="taskFactory">The asynchronous function used to produce the value when it is needed.</param>
    public AsyncLazy(Func<Task<T>> taskFactory)
        : base(() => Task.Factory.StartNew(taskFactory).Unwrap())
    {
    }
}
