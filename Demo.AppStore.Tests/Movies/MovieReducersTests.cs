using System.Collections.Immutable;
using FluentAssertions;

namespace Demo.AppStore.Tests.Movies;

public class MovieReducersTests
{
    private readonly MovieReducers _sut = new();

    private readonly MovieState _initialState = new()
    {
        Movies = [],
        IsLoading = false,
        ErrorMessage = null,
        Pagination = new Pagination
        {
            CurrentPage = 1,
            TotalPages = 1,
            TotalItems = 0
        }
    };
    
    private const string Key = "movie";

    [Fact]
    public void MovieReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void MovieReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void LoadMovies_ShouldSetIsLoadingToTrue()
    {
        // Act
        var newState = _sut.Reduce(_initialState, new LoadMovies());

        // Assert
        newState.IsLoading.Should().BeTrue();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesSuccess_ShouldSetMoviesAndIsLoadingToFalse()
    {
        // Arrange
        var movies = ImmutableArray.Create(MoviesExamples.Movies[0]);

        // Act
        var newState = _sut.Reduce(_initialState, new LoadMoviesSuccess(movies, movies.Length));

        // Assert
        newState.Movies.Should().BeEquivalentTo(movies);
        newState.IsLoading.Should().BeFalse();
        newState.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadMoviesFailure_ShouldSetErrorMessageAndIsLoadingToFalse()
    {
        // Arrange
        const string errorMessage = "Failed to load movies.";

        // Act
        var newState = _sut.Reduce(_initialState, new LoadMoviesFailure(errorMessage));

        // Assert
        newState.ErrorMessage.Should().Be(errorMessage);
        newState.IsLoading.Should().BeFalse();
        newState.Movies.Should().BeEmpty();
    }

    [Fact]
    public void SelectMovieCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var state = _initialState with
        {
            Movies =
            [
                MoviesExamples.Movies[0],
                MoviesExamples.Movies[1]
            ]
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
        var state = _initialState with
        {
            Movies =
            [
                MoviesExamples.Movies[0],
                MoviesExamples.Movies[1]
            ]
        };

        // Act
        var sortedMovies = state.SelectMoviesByYear();

        // Assert
        sortedMovies.Should().BeInDescendingOrder(movie => movie.Year);
    }
}