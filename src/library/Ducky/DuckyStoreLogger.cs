using Ducky.Pipeline;
using Microsoft.Extensions.Logging;

namespace Ducky;

/// <summary>
/// Observes and logs store events.
/// </summary>
public class DuckyStoreLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly IStoreEventPublisher _eventPublisher;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStoreLogger"/> class and subscribes to pipeline events.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public DuckyStoreLogger(
        ILogger<DuckyStoreLogger> logger,
        IStoreEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _logger = logger;
        _eventPublisher = eventPublisher;
        _eventPublisher.EventPublished += OnEventPublished;
    }

    private void OnEventPublished(object? sender, StoreEventArgs e)
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

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="DuckyStoreLogger"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">If true, the method has been called directly or indirectly by a user's code.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _eventPublisher.EventPublished -= OnEventPublished;
        }

        _disposed = true;
    }
}
