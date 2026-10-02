namespace Ducky;

/// <summary>Helpers for effect handlers (EFF-02).</summary>
public static class EffectContextExtensions
{
    /// <summary>
    /// Dispatches <paramref name="started"/>, awaits <paramref name="work"/>, then dispatches
    /// <paramref name="succeeded"/>(result). Any exception from <paramref name="work"/> other than an
    /// <see cref="OperationCanceledException"/> for this run's own token dispatches <paramref name="failed"/>(exception),
    /// and the run then ends normally; an <see cref="HttpClient"/> timeout is such a failure. Cancellation of this run's
    /// token propagates as cancellation, with no action.
    /// </summary>
    /// <typeparam name="TResult">The result of <paramref name="work"/>.</typeparam>
    /// <param name="context">The run's context.</param>
    /// <param name="started">The action dispatched before the work starts.</param>
    /// <param name="work">The work, given <paramref name="cancellationToken"/>.</param>
    /// <param name="succeeded">Maps the result to the action dispatched on success.</param>
    /// <param name="failed">Maps the exception to the action dispatched on failure.</param>
    /// <param name="cancellationToken">The handler's token, passed to <paramref name="work"/>.</param>
    /// <returns>A task that completes when the outcome action was dispatched, or is canceled with the run.</returns>
    /// <exception cref="ArgumentNullException">An argument is null; thrown before anything is dispatched.</exception>
    /// <remarks>
    /// A throw from <paramref name="succeeded"/> or <paramref name="failed"/> is not caught: like any throw from the
    /// handler, it dispatches <see cref="EffectFailed"/>.
    /// </remarks>
#pragma warning disable VSTHRD200 // justification: Run is the normative name of SPEC §5.5
    public static Task Run<TResult>(
        this EffectContext context,
        object started,
        Func<CancellationToken, Task<TResult>> work,
        Func<TResult, object> succeeded,
        Func<Exception, object> failed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(succeeded);
        ArgumentNullException.ThrowIfNull(failed);
        return RunCoreAsync(context, started, work, succeeded, failed, cancellationToken);
    }
#pragma warning restore VSTHRD200

    // Dispatch, not an awaited DispatchAsync, as for any handler: the run never waits for its own actions to be reduced.
#pragma warning disable VSTHRD103 // justification: EffectContext.Dispatch enqueues and returns; SPEC §5.5 dispatches the triad
    private static async Task RunCoreAsync<TResult>(
        EffectContext context,
        object started,
        Func<CancellationToken, Task<TResult>> work,
        Func<TResult, object> succeeded,
        Func<Exception, object> failed,
        CancellationToken cancellationToken)
    {
        context.Dispatch(started);
        TResult result;
        try
        {
            result = await work(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
        }
#pragma warning disable CA1031 // justification: every failure of the work becomes failed(ex) (SPEC §5.5); only this run's cancellation propagates
        catch (Exception ex) when (ex is not OperationCanceledException oce || !context.Run.IsCancellation(oce))
#pragma warning restore CA1031
        {
            context.Dispatch(failed(ex));
            return;
        }

        context.Dispatch(succeeded(result));
    }
#pragma warning restore VSTHRD103
}
