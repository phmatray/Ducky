// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that debounces search queries to avoid excessive API calls.
/// </summary>
public class DebouncedSearchEffect : AsyncEffect<SearchMovies>
{
    private readonly IMoviesService _moviesService;
    private readonly ILogger<DebouncedSearchEffect> _logger;
    private static CancellationTokenSource? _searchCancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedSearchEffect"/> class.
    /// </summary>
    /// <param name="moviesService">The movies service.</param>
    /// <param name="logger">The logger.</param>
    public DebouncedSearchEffect(
        IMoviesService moviesService,
        ILogger<DebouncedSearchEffect> logger)
    {
        _moviesService = moviesService ?? throw new ArgumentNullException(nameof(moviesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public override async Task HandleAsync(SearchMovies action, IRootState rootState)
    {
        // Cancel any previous search
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Wait 500ms for debouncing
            await Task.Delay(500, _searchCancellationTokenSource.Token);

            // For search, we'll just load the first page of movies
            GetMoviesResponse response = await _moviesService.GetMoviesAsync(
                1, 20, _searchCancellationTokenSource.Token);

            List<Movie> filteredMovies = string.IsNullOrWhiteSpace(action.Query)
                ? response.Movies.ToList()
                : response.Movies.Where(m =>
                    m.Title.Contains(action.Query, StringComparison.OrdinalIgnoreCase)
                        || m.Director.Contains(action.Query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            Dispatcher.LoadMoviesSuccess(filteredMovies, filteredMovies.Count);
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled due to a new search, this is expected
            _logger.LogDebug("Search for '{Query}' was cancelled", action.Query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching movies");
            Dispatcher.LoadMoviesFailure(ex.Message);
        }
    }
}
