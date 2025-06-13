namespace Demo.ConsoleAppReactive.Effects;

public class SearchDebouncedEffect : DebouncedEffect<SearchQuery>
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<SearchDebouncedEffect> _logger;
    private readonly Random _random = new();

    public SearchDebouncedEffect(
        IDispatcher dispatcher,
        ILogger<SearchDebouncedEffect> logger)
        : base(TimeSpan.FromMilliseconds(500))
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override IObservable<object> ProcessDebouncedAction(
        SearchQuery action,
        IObservable<IRootState> rootState)
    {
        return Observable.FromAsync(async () =>
        {
            _logger.LogInformation("Processing debounced search for: {Query}", action.Query);

            _dispatcher.Dispatch(new SearchStarted(action.Query));

            // Simulate search delay
            await Task.Delay(200 + _random.Next(300)).ConfigureAwait(false);

            // Simulate search results
            int resultCount = string.IsNullOrWhiteSpace(action.Query)
                ? 0
                : _random.Next(1, 100);

            return new SearchCompleted(action.Query, resultCount);
        });
    }
}
