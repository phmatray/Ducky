// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Movies;

public sealed class MoviesReducersTests : IDisposable
{
    private const string Key = "movies";

    private readonly MoviesReducers _sut = new();

    private readonly MoviesState _initialState = new()
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

    private bool _disposed;

    [Fact]
    public void MovieReducers_Should_Return_Initial_State()
    {
        // Act
        MoviesState initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void MovieReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void MovieReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<MoviesState>();
    }

    [Fact]
    public void MovieReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<MoviesState, IAction, MoviesState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(4);
    }

    [Fact]
    public void LoadMovies_ShouldSetIsLoadingToTrue()
    {
        // Act
        MoviesState newState = _sut.Reduce(_initialState, new LoadMovies());

        // Assert
        newState.IsLoading.Should().BeTrue();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesSuccess_ShouldSetMoviesAndIsLoadingToFalse()
    {
        // Arrange
        ImmutableArray<Movie> movies = [MoviesExamples.Movies[0]];

        // Act
        MoviesState newState = _sut.Reduce(_initialState, new LoadMoviesSuccess(movies, movies.Length));

        // Assert
        newState.Movies.Values.Should().BeEquivalentTo(movies);
        newState.IsLoading.Should().BeFalse();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesFailure_ShouldSetErrorMessageAndIsLoadingToFalse()
    {
        // Arrange
        const string errorMessage = "Failed to load movies.";
        DuckyException exception = new(errorMessage);

        // Act
        MoviesState newState = _sut.Reduce(_initialState, new LoadMoviesFailure(exception));

        // Assert
        newState.ErrorMessage.Should().Be(errorMessage);
        newState.IsLoading.Should().BeFalse();
        newState.Movies.Should().BeEmpty();
    }

    [Fact]
    public void SelectMovieCount_ShouldReturnCorrectCount()
    {
        // Arrange
        MoviesState state = _initialState with
        {
            Movies = ImmutableDictionary<int, Movie>.Empty
                .Add(1, MoviesExamples.Movies[0])
                .Add(2, MoviesExamples.Movies[1])
        };

        // Act
        int movieCount = state.SelectMovieCount();

        // Assert
        movieCount.Should().Be(2);
    }

    [Fact]
    public void SelectMoviesByYear_ShouldReturnMoviesSortedByYearDescending()
    {
        // Arrange
        MoviesState state = _initialState with
        {
            Movies = ImmutableDictionary<int, Movie>.Empty
                .Add(1, MoviesExamples.Movies[0])
                .Add(2, MoviesExamples.Movies[1])
        };

        // Act
        IEnumerable<Movie> sortedMovies = state.SelectMoviesByYear().Values;

        // Assert
        sortedMovies.Should().BeInDescendingOrder(movie => movie.Year);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
