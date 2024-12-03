// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Movies;

#region State

public record Pagination
{
    public int CurrentPage { get; init; }

    public int TotalPages { get; init; }

    public int TotalItems { get; init; }
}

public record MoviesState
{
    public required ImmutableDictionary<int, Movie> Movies { get; init; }

    public required bool IsLoading { get; init; }

    public required string? ErrorMessage { get; init; }

    public required Pagination Pagination { get; init; }

    // Selectors
    // ==========
    // We can define selectors as methods in the state record
    // to encapsulate the logic of selecting data from the state.
    // Each method should begin with the word "Select".
    public int SelectMovieCount()
    {
        return Movies.Count;
    }

    public Movie? SelectMovieById(int id)
    {
        return Movies.GetValueOrDefault(id);
    }

    public ImmutableDictionary<int, Movie> SelectMoviesByYear()
    {
        return Movies
            .OrderByDescending(pair => pair.Value.Year)
            .ToImmutableDictionary();
    }
}

#endregion

#region Actions

public record LoadMovies : IAction;

public record LoadMoviesSuccess(ValueCollection<Movie> Movies, int TotalItems) : IAction;

public record LoadMoviesFailure(Exception Error) : IAction;

public record SetCurrentPage(int CurrentPage) : IAction;

#endregion

#region Reducers

public record MoviesReducers : SliceReducers<MoviesState>
{
    public MoviesReducers()
    {
        On<LoadMovies>((state, _)
            => state with { IsLoading = true, ErrorMessage = null });

        On<LoadMoviesSuccess>((state, action)
            => state with
            {
                Movies = action.Movies.ToImmutableDictionary(movie => movie.Id),
                IsLoading = false,
                Pagination = state.Pagination with
                {
                    TotalItems = action.TotalItems,
                    TotalPages = (int)Math.Ceiling(action.TotalItems / 5.0)
                }
            });

        On<LoadMoviesFailure>((state, action)
            => state with
            {
                Movies = ImmutableDictionary<int, Movie>.Empty,
                ErrorMessage = action.Error.Message,
                IsLoading = false
            });

        On<SetCurrentPage>((state, action)
            => state with { Pagination = state.Pagination with { CurrentPage = action.CurrentPage } });
    }

    public override MoviesState GetInitialState()
    {
        return new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination
            {
                CurrentPage = 1,
                TotalPages = 1,
                TotalItems = 0
            }
        };
    }
}

#endregion

#region Effects

// ReSharper disable once UnusedType.Global
public class LoadMoviesEffect(IMoviesService moviesService) : ReactiveEffect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<LoadMovies>()
            .LogMessage("Loading movies...")
            .WithSliceState<MoviesState, LoadMovies>(rootState)
            .InvokeService(
                pair => moviesService.GetMoviesAsync(pair.State.Pagination.CurrentPage, 5),
                response => new LoadMoviesSuccess(response.Movies, response.TotalItems),
                ex => new LoadMoviesFailure(ex))
            .LogMessage("Movies loaded.");

        // THE FOLLOWING CODE WORKS AS AN ALTERNATIVE TO THE ABOVE CODE
        // ============================================================
        // return actions
        //     .OfType<IAction, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .WithSliceState<MoviesState, LoadMovies>(rootState)
        //     .SelectAwait(async (pair, ct) =>
        //     {
        //         try
        //         {
        //             const int pageSize = 5;
        //             var state = pair.State;
        //             var currentPage = state.Pagination.CurrentPage;
        //             var response = await moviesService.GetMoviesAsync(currentPage, pageSize, ct);
        //             return new LoadMoviesSuccess(response.Movies, response.TotalItems) as IAction;
        //         }
        //         catch (Exception ex)
        //         {
        //             return new LoadMoviesFailure(ex);
        //         }
        //     });
    }
}

#endregion
