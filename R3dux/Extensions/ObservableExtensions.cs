using R3;

namespace R3dux;

/// <summary>
/// Provides extension methods for observable sequences.
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    /// Returns the first element of an observable sequence synchronously.
    /// </summary>
    /// <param name="observable">The observable sequence to return the first element of.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element of the observable sequence.</returns>
    public static T FirstSync<T>(this Observable<T> observable)
    {
        var stateAsync = observable.FirstAsync();
        stateAsync.Wait();
        return stateAsync.Result;
    } 
}