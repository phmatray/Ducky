namespace Bustand.Middleware;

/// <summary>
/// Configuration options for the logging middleware.
/// </summary>
public class LoggingMiddlewareOptions
{
    /// <summary>
    /// If set, only log changes from stores whose types are in this set.
    /// If null, all stores are logged (unless excluded).
    /// </summary>
    public HashSet<Type>? IncludeStores { get; set; }

    /// <summary>
    /// Stores to exclude from logging. Takes precedence over IncludeStores.
    /// </summary>
    public HashSet<Type>? ExcludeStores { get; set; }

    /// <summary>
    /// Maximum number of differences to include in log output.
    /// Default: 100. Set lower for large state objects.
    /// </summary>
    public int MaxDifferences { get; set; } = 100;

    /// <summary>
    /// Determines if the specified store type should be logged.
    /// </summary>
    /// <param name="storeType">The store type to check.</param>
    /// <returns>True if the store should be logged.</returns>
    public bool ShouldLog(Type storeType)
    {
        if (ExcludeStores?.Contains(storeType) == true)
            return false;
        if (IncludeStores != null && !IncludeStores.Contains(storeType))
            return false;
        return true;
    }
}
