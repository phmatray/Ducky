using System.Diagnostics;

namespace Bustand.Persistence;

/// <summary>
/// Batches rapid state changes into single storage writes using a debounce timer.
/// </summary>
/// <typeparam name="TState">The state type being persisted.</typeparam>
/// <remarks>
/// <para>
/// When <see cref="QueueWrite"/> is called, the writer waits for the debounce period
/// before actually writing. If another call comes in during the wait, the timer resets.
/// This batches rapid changes (e.g., typing) into a single write.
/// </para>
/// <para>
/// <b>Thread safety:</b> This class is thread-safe. Multiple concurrent QueueWrite calls
/// are handled correctly via locking.
/// </para>
/// <para>
/// <b>Disposal:</b> Always dispose this class to prevent timer leaks. The timer callback
/// may still fire once after disposal for any pending write.
/// </para>
/// </remarks>
public sealed class DebouncedWriter<TState> : IDisposable where TState : class
{
    private readonly Func<TState, Task> _writeAction;
    private readonly TimeSpan _debounceDelay;
    private readonly object _lock = new();
    private Timer? _timer;
    private TState? _pendingState;
    private bool _disposed;

    /// <summary>
    /// Creates a new debounced writer.
    /// </summary>
    /// <param name="writeAction">The async action to invoke when writing.</param>
    /// <param name="debounceMs">Debounce delay in milliseconds.</param>
    public DebouncedWriter(Func<TState, Task> writeAction, int debounceMs = 300)
    {
        ArgumentNullException.ThrowIfNull(writeAction);
        if (debounceMs < 0)
            throw new ArgumentOutOfRangeException(nameof(debounceMs), "Debounce delay must be non-negative.");

        _writeAction = writeAction;
        _debounceDelay = TimeSpan.FromMilliseconds(debounceMs);
    }

    /// <summary>
    /// Queues a state for writing. Resets the debounce timer if one is active.
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <remarks>
    /// This method is non-blocking and returns immediately. The actual write
    /// happens asynchronously after the debounce delay.
    /// </remarks>
    public void QueueWrite(TState state)
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            _pendingState = state;

            // Cancel existing timer and start a new one
            _timer?.Dispose();
            _timer = new Timer(
                callback: FlushCallback,
                state: null,
                dueTime: _debounceDelay,
                period: Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Forces an immediate write of any pending state.
    /// Use before disposal to ensure no data loss.
    /// </summary>
    public async Task FlushAsync()
    {
        TState? stateToWrite;

        lock (_lock)
        {
            stateToWrite = _pendingState;
            _pendingState = null;
            _timer?.Dispose();
            _timer = null;
        }

        if (stateToWrite is not null)
        {
            try
            {
                await _writeAction(stateToWrite);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bustand] Debounced write flush failed: {ex.Message}");
            }
        }
    }

    private async void FlushCallback(object? _)
    {
        if (_disposed)
            return;

        TState? stateToWrite;

        lock (_lock)
        {
            if (_disposed)
                return;

            stateToWrite = _pendingState;
            _pendingState = null;
        }

        if (stateToWrite is not null)
        {
            try
            {
                await _writeAction(stateToWrite);
            }
            catch (Exception ex)
            {
                // Log but don't throw - this runs on timer thread
                Debug.WriteLine($"[Bustand] Debounced write failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Disposes the writer and its timer.
    /// Does NOT flush pending state - call <see cref="FlushAsync"/> first if needed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            _pendingState = default;
        }
    }
}
