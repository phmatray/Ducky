// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.ReactiveEffect;
using R3;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that debounces search queries to avoid excessive API calls.
/// </summary>
public class DebouncedSearchEffect : ReactiveEffect
{
    private readonly IMoviesService _moviesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedSearchEffect"/> class.
    /// </summary>
    /// <param name="moviesService">The movies service.</param>
    public DebouncedSearchEffect(IMoviesService moviesService)
    {
        _moviesService = moviesService ?? throw new ArgumentNullException(nameof(moviesService));
    }

    /// <inheritdoc />
    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<SearchMovies>()
            .Debounce(TimeSpan.FromMilliseconds(500)) // Wait 500ms after last keystroke
            .DistinctUntilChanged(action => action.Query) // Only search if query changed
            .SelectAwait(async action =>
            {
                try
                {
                    // Simulate filtering movies based on search query
                    List<Movie> allMovies = await _moviesService.GetMoviesAsync();
                    List<Movie> filteredMovies = string.IsNullOrWhiteSpace(action.Query)
                        ? allMovies
                        : allMovies.Where(m => 
                            m.Title.Contains(action.Query, StringComparison.OrdinalIgnoreCase)
                                || m.Director.Contains(action.Query, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                    return (object)new LoadMoviesSuccess(filteredMovies);
                }
                catch (Exception ex)
                {
                    return (object)new LoadMoviesFailure(ex.Message);
                }
            });
    }
}
