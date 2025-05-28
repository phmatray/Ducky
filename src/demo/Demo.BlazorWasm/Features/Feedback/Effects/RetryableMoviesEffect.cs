using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Example of an async effect that might fail and benefit from retry logic.
/// </summary>
public sealed class RetryableMoviesEffect : AsyncEffect<LoadMovies>
{
    private readonly IMoviesService _moviesService;
    private readonly ILogger<RetryableMoviesEffect> _logger;
    private static int _attemptCount;

    public RetryableMoviesEffect(
        IMoviesService moviesService,
        ILogger<RetryableMoviesEffect> logger)
    {
        _moviesService = moviesService;
        _logger = logger;
    }

    public override async Task HandleAsync(LoadMovies action, IRootState rootState)
    {
        try
        {
            _logger.LogInformation("Loading movies with retry (attempt {Attempt})...", ++_attemptCount);

            // Simulate intermittent failures for demonstration
            if (_attemptCount < 3 && Random.Shared.NextDouble() < 0.7)
            {
                throw new HttpRequestException("Simulated network failure");
            }

            // Get current pagination from state
            MoviesState moviesState = rootState.GetSliceState<MoviesState>();
            int currentPage = moviesState.Pagination.CurrentPage;
            const int pageSize = 5;

            GetMoviesResponse response = await _moviesService.GetMoviesAsync(currentPage, pageSize);

            Dispatcher?.Dispatch(new LoadMoviesSuccess(response.Movies, response.TotalItems));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load movies");

            // Re-throw to trigger retry logic
            throw;
        }
    }
}
