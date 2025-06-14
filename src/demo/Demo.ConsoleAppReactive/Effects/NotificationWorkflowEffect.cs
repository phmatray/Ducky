namespace Demo.ConsoleAppReactive.Effects;

public class NotificationWorkflowEffect : WorkflowEffect<StartNotificationWorkflow>
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<NotificationWorkflowEffect> _logger;

    public NotificationWorkflowEffect(
        IDispatcher dispatcher,
        ILogger<NotificationWorkflowEffect> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override IObservable<object> ExecuteWorkflow(
        StartNotificationWorkflow startAction,
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        Guid notificationId = Guid.NewGuid();

        return StepAsync(
            "Create notification",
            () =>
            {
                _dispatcher.Dispatch(new NotificationCreated(
                    notificationId,
                    startAction.Title,
                    startAction.Message,
                    startAction.Type));
                return new object();
            })
            .Concat(
                // Step 1: Create notification
                // Step 2: Process notification  
                StepAsync(
                    "Process notification",
                    async () =>
                    {
                        await Task.Delay(500).ConfigureAwait(false); // Simulate processing
                        return (object)new NotificationProcessed(notificationId);
                    })
                );
    }

    private IObservable<T> StepAsync<T>(string name, Func<T> action)
    {
        return Observable.Defer(() =>
        {
            try
            {
                _logger.LogInformation("Executing step: {StepName}", name);
                T result = action();
                return Observable.Return(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Step failed: {StepName}", name);
                return Observable.Throw<T>(ex);
            }
        });
    }

    private IObservable<T> StepAsync<T>(string name, Func<Task<T>> action)
    {
        return Observable.FromAsync(async () =>
        {
            try
            {
                _logger.LogInformation("Executing async step: {StepName}", name);
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Async step failed: {StepName}", name);
                throw;
            }
        });
    }
}
