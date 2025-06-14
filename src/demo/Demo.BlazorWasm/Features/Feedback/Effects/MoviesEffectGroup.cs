// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Groups all movie-related effects with shared dependencies and helper methods.
/// </summary>
public class MoviesEffectGroup : AsyncEffectGroup
{
    private readonly IMoviesService _moviesService;
    private readonly ILogger<MoviesEffectGroup> _logger;
    private readonly ISnackbar _snackbar;

    public MoviesEffectGroup(
        IMoviesService moviesService,
        ILogger<MoviesEffectGroup> logger,
        ISnackbar snackbar)
    {
        _moviesService = moviesService;
        _logger = logger;
        _snackbar = snackbar;

        On<LoadMovies>(HandleLoadMoviesAsync);
        On<SearchMovies>(HandleSearchMoviesAsync);
        On<LoadMoviesSuccess>(HandleLoadMoviesSuccessAsync);
        On<LoadMoviesFailure>(HandleLoadMoviesFailureAsync);
    }

    private async Task HandleLoadMoviesAsync(LoadMovies action, IStateProvider stateProvider)
    {
        try
        {
            (int currentPage, int pageSize) = GetPaginationInfo(stateProvider);
            GetMoviesResponse response = await LoadMoviesFromServiceAsync(currentPage, pageSize);

            Dispatcher.LoadMoviesSuccess(response.Movies, response.TotalItems);
        }
        catch (Exception ex)
        {
            LogAndDispatchError(ex, "Failed to load movies");
        }
    }

    private async Task HandleSearchMoviesAsync(SearchMovies action, IStateProvider stateProvider)
    {
        try
        {
            (int currentPage, int pageSize) = GetPaginationInfo(stateProvider);
            GetMoviesResponse response = await LoadMoviesFromServiceAsync(currentPage, pageSize);

            List<Movie> filteredMovies = FilterMovies(response.Movies, action.Query);

            Dispatcher.LoadMoviesSuccess(filteredMovies, filteredMovies.Count);
        }
        catch (Exception ex)
        {
            LogAndDispatchError(ex, $"Error searching movies with query '{action.Query}'");
        }
    }

    private Task HandleLoadMoviesSuccessAsync(LoadMoviesSuccess action, IStateProvider stateProvider)
    {
        string message = $"Loaded {action.Movies.Count} movies from the server.";
        ShowSuccessNotification(message);
        return Task.CompletedTask;
    }

    private Task HandleLoadMoviesFailureAsync(LoadMoviesFailure action, IStateProvider stateProvider)
    {
        ShowErrorNotification(action.ErrorMessage);
        return Task.CompletedTask;
    }

    // Shared helper methods
    private (int currentPage, int pageSize) GetPaginationInfo(IStateProvider stateProvider)
    {
        MoviesState moviesState = stateProvider.GetSlice<MoviesState>();
        return (moviesState.Pagination.CurrentPage, 5);
    }

    private async Task<GetMoviesResponse> LoadMoviesFromServiceAsync(int currentPage, int pageSize)
    {
        _logger.LogInformation("Loading movies - Page: {Page}, Size: {Size}", currentPage, pageSize);
        return await _moviesService.GetMoviesAsync(currentPage, pageSize);
    }

    private List<Movie> FilterMovies(IEnumerable<Movie> movies, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return movies.ToList();
        }

        return movies.Where(m =>
            m.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || m.Director.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void LogAndDispatchError(Exception ex, string message)
    {
        _logger.LogError(ex, message);
        Dispatcher.LoadMoviesFailure(ex.Message);
    }

    private void ShowSuccessNotification(string message)
    {
        _snackbar.Add(message, Severity.Success);
        SuccessNotification notification = new(message);
        Dispatcher.AddNotification(notification);
    }

    private void ShowErrorNotification(string message)
    {
        _snackbar.Add(message, Severity.Error);
        ErrorNotification notification = new(message);
        Dispatcher.AddNotification(notification);
    }
}
