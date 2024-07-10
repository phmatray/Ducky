using System.Collections.Immutable;
using FluentAssertions;
using R3dux;

namespace Demo.AppStore.Tests.Movies;

public class MovieReducersTests
{
    private readonly MovieReducers _reducers = new();

    [Fact]
    public void LoadMovies_ShouldSetIsLoadingToTrue()
    {
        // Arrange
        var initialState = new MovieState
        {
            Movies = [],
            IsLoading = false,
            ErrorMessage = null
        };

        // Act
        var newState = _reducers.Reduce(initialState, new LoadMovies());

        // Assert
        newState.IsLoading.Should().BeTrue();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesSuccess_ShouldSetMoviesAndIsLoadingToFalse()
    {
        // Arrange
        var initialState = new MovieState
        {
            Movies = [],
            IsLoading = true,
            ErrorMessage = null
        };

        var movies = ImmutableArray.Create(MoviesExamples.Movies[0]);

        // Act
        var newState = _reducers.Reduce(initialState, new LoadMoviesSuccess(movies));

        // Assert
        newState.Movies.Should().BeEquivalentTo(movies);
        newState.IsLoading.Should().BeFalse();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesFailure_ShouldSetErrorMessageAndIsLoadingToFalse()
    {
        // Arrange
        var initialState = new MovieState
        {
            Movies = [],
            IsLoading = true,
            ErrorMessage = null
        };

        const string errorMessage = "Failed to load movies.";

        // Act
        var newState = _reducers.Reduce(initialState, new LoadMoviesFailure(errorMessage));

        // Assert
        newState.ErrorMessage.Should().Be(errorMessage);
        newState.IsLoading.Should().BeFalse();
        newState.Movies.Should().BeEmpty();
    }

    [Fact]
    public void SelectMovieCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var state = new MovieState
        {
            Movies = [MoviesExamples.Movies[0], MoviesExamples.Movies[1]],
            IsLoading = false,
            ErrorMessage = null
        };

        // Act
        var movieCount = state.SelectMovieCount();

        // Assert
        movieCount.Should().Be(2);
    }

    [Fact]
    public void SelectMoviesByYear_ShouldReturnMoviesSortedByYearDescending()
    {
        // Arrange
        var state = new MovieState
        {
            Movies = [MoviesExamples.Movies[0], MoviesExamples.Movies[1]],
            IsLoading = false,
            ErrorMessage = null
        };

        // Act
        var sortedMovies = state.SelectMoviesByYear();

        // Assert
        sortedMovies.Should().BeInDescendingOrder(movie => movie.Year);
    }
}