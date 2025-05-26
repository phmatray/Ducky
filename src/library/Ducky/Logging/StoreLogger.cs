using Ducky.Pipeline;
using Microsoft.Extensions.Logging;
using R3;

namespace Ducky;

/// <summary>
/// Observes and logs store events.
/// </summary>
public class StoreLogger : Observer<StoreEventArgs>
{
    private readonly ILogger _logger;
    private readonly IDisposable _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreLogger"/> class and subscribes to pipeline events via R3.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public StoreLogger(
        ILogger<StoreLogger> logger,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _logger = logger;
        _subscription = eventPublisher.Events.Subscribe(this);
    }

    /// <inheritdoc />
    protected override void OnNextCore(StoreEventArgs e)
    {
        switch (e)
        {
            case StoreInitializedEventArgs initialized:
            {
                _logger.LogInformation(
                    "[STORE] Store initialized with {SliceCount} slices: {SliceKeys}",
                    initialized.SliceCount,
                    string.Join(", ", initialized.SliceKeys));
                break;
            }
            case SliceAddedEventArgs sliceAdded:
            {
                _logger.LogDebug(
                    "[STORE] Slice \"{SliceKey}\" of type {SliceType} added",
                    sliceAdded.SliceKey,
                    sliceAdded.SliceType.Name);
                break;
            }
            case StoreDisposingEventArgs disposing:
            {
                _logger.LogInformation(
                    "[STORE] Disposing store after {Uptime} uptime",
                    disposing.Uptime);
                break;
            }
            case ActionStartedEventArgs started:
            {
                _logger.LogInformation(
                    "[EVENT] Action started: {ActionType}",
                    started.Context.Action.GetType().Name);
                break;
            }
            case ActionCompletedEventArgs completed:
            {
                _logger.LogInformation(
                    "[EVENT] Action completed: {ActionType}",
                    completed.Context.Action.GetType().Name);
                break;
            }
            case MiddlewareStartedEvent startedMw:
            {
                _logger.LogInformation(
                    "[EVENT] Middleware {Phase} started: {Middleware} for {ActionType}",
                    startedMw.Phase,
                    startedMw.Middleware.GetType().Name,
                    startedMw.Context.Action.GetType().Name);
                break;
            }
            case MiddlewareCompletedEventArgs completedMw:
            {
                _logger.LogInformation(
                    "[EVENT] Middleware {Phase} completed: {Middleware} for {ActionType}",
                    completedMw.Phase,
                    completedMw.Middleware.GetType().Name,
                    completedMw.Context.Action.GetType().Name);
                break;
            }
            case MiddlewareErroredEventArgs erroredMw:
            {
                _logger.LogError(
                    "[EVENT] Middleware {Phase} error: {Middleware}: {Exception}",
                    erroredMw.Phase,
                    erroredMw.Middleware.GetType().Name,
                    erroredMw.Exception);
                break;
            }
            case ActionAbortedEventArgs aborted:
            {
                _logger.LogWarning(
                    "[EVENT] Action aborted: {ActionType}, Reason: {Reason}",
                    aborted.Context.Action.GetType().Name,
                    aborted.Reason);
                break;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnErrorResumeCore(Exception error)
    {
        _logger.LogError(error, "[EVENT] Error in event stream");
    }

    /// <inheritdoc />
    protected override void OnCompletedCore(Result result)
    {
        _logger.LogInformation("[EVENT] Event stream completed");
    }

    /// <inheritdoc />
    protected override void DisposeCore()
    {
        _subscription.Dispose();
    }
}
