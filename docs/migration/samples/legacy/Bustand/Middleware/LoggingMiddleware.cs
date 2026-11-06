using KellermanSoftware.CompareNetObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bustand.Middleware;

/// <summary>
/// Middleware that logs state changes with diff information.
/// Uses CompareNETObjects for state diffing and ILogger for output.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
public class LoggingMiddleware<TState> : IMiddleware<TState> where TState : class
{
    private readonly ILogger<LoggingMiddleware<TState>> _logger;
    private readonly LoggingMiddlewareOptions _options;
    private readonly CompareLogic _comparer;

    /// <summary>
    /// Creates a new logging middleware instance.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Optional logging configuration.</param>
    public LoggingMiddleware(
        ILogger<LoggingMiddleware<TState>> logger,
        IOptions<LoggingMiddlewareOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value ?? new LoggingMiddlewareOptions();
        _comparer = new CompareLogic(new ComparisonConfig
        {
            MaxDifferences = _options.MaxDifferences
        });
    }

    /// <inheritdoc />
    public bool OnBeforeChange(MiddlewareContext<TState> context)
    {
        // Logging middleware doesn't block changes
        return true;
    }

    /// <inheritdoc />
    public void OnAfterChange(MiddlewareContext<TState> context)
    {
        // Early exit if logging disabled or store filtered out
        if (!_logger.IsEnabled(LogLevel.Debug))
            return;

        if (!_options.ShouldLog(context.StoreType))
            return;

        // Compare states to find differences
        var result = _comparer.Compare(context.OldState, context.NewState);

        if (!result.AreEqual)
        {
            _logger.LogStateChange(
                context.StoreType.Name,
                context.ActionName ?? "Unknown",
                result.DifferencesString);
        }
    }
}
