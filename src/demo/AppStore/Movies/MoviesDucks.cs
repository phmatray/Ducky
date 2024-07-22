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

    public ImmutableDictionary<int, Movie> SelectMoviesByYear()
    {
        return Movies
            .OrderByDescending(pair => pair.Value.Year)
            .ToImmutableDictionary();
    }
}

#endregion

#region Actions

public record LoadMovies(int PageNumber = 1, int PageSize = 5) : IAction;

public record LoadMoviesSuccess(ImmutableDictionary<int, Movie> Movies, int TotalItems) : IAction;

public record LoadMoviesFailure(Exception Error) : IAction;

public record SetCurrentPage(int CurrentPage) : IAction;

#endregion

#region Reducers

public record MoviesReducers : SliceReducers<MoviesState>
{
    public MoviesReducers()
    {
        Map<LoadMovies>((state, _)
            => state with { IsLoading = true, ErrorMessage = null });

        Map<LoadMoviesSuccess>((state, action)
            => state with
            {
                Movies = action.Movies,
                IsLoading = false,
                Pagination = state.Pagination with
                {
                    TotalItems = action.TotalItems,
                    TotalPages = (int)Math.Ceiling(action.TotalItems / 5.0)
                }
            });

        Map<LoadMoviesFailure>((state, action)
            => state with
            {
                Movies = ImmutableDictionary<int, Movie>.Empty,
                ErrorMessage = action.Error.Message,
                IsLoading = false
            });

        Map<SetCurrentPage>((state, action)
            => state with { Pagination = state.Pagination with { CurrentPage = action.CurrentPage } });
    }

    public override MoviesState GetInitialState()
    {
        return new MoviesState
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
public class LoadMoviesEffect(IMoviesService moviesService) : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState)
    {
        // return actions
        //     .OfType<LoadMovies>()
        //     .LogMessage("Loading movies...")
        //     .InvokeService(
        //         action => moviesService.GetMoviesAsync(action.PageNumber, action.PageSize),
        //         movies => new LoadMoviesSuccess(movies),
        //         ex => new LoadMoviesFailure(ex.Message))
        //     .LogMessage("Movies loaded.");

        // THE SAME CODE CAN BE WRITTEN WITHOUT THE EXTENSION METHODS
        // ============================================================
        // return actions
        //     .OfType<IAction, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .SelectMany(action => moviesService
        //         .GetMoviesAsync()
        //         .ToObservable()
        //         .Select(movies => (object)new LoadMoviesSuccess(movies))
        //         .Do(_ => Console.WriteLine("Movies loaded."))
        //         .Catch<object, Exception>(ex => Observable.Return<object>(new LoadMoviesFailure(ex.Message))));

        // THE FOLLOWING CODE WORKS AS AN ALTERNATIVE TO THE ABOVE CODE
        // ============================================================
        return actions
            .OfType<LoadMovies>()
            .Do(_ => Console.WriteLine("Loading movies..."))
            .WithSliceState<MoviesState, LoadMovies>(rootState)
            .SelectAwait(async (pair, ct) =>
            {
                try
                {
                    // 1/3 chance of failing
                    if (Random.Shared.Next(3) == 0)
                    {
                        throw new MovieException("Failed to load movies");
                    }

                    var (state, action) = pair;
                    var currentPage = state.Pagination.CurrentPage;
                    var response = await moviesService.GetMoviesAsync(currentPage, action.PageSize, ct);
                    var dictionary = response.Movies.ToImmutableDictionary(movie => movie.Id);
                    return new LoadMoviesSuccess(dictionary, response.TotalItems) as IAction;
                }
                catch (Exception ex)
                {
                    return new LoadMoviesFailure(ex);
                }
            });
    }
}

#endregion
