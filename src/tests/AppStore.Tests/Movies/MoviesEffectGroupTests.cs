using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace AppStore.Tests.Movies;

public class MoviesEffectGroupTests
{
    private readonly Mock<IMoviesService> _moviesServiceMock = new();
    private readonly Mock<ILogger<MoviesEffectGroup>> _loggerMock = new();
    private readonly Mock<ISnackbar> _snackbarMock = new();
    private readonly Mock<IDispatcher> _dispatcherMock = new();
    private readonly Mock<IRootState> _rootStateMock = new();
    private readonly MoviesEffectGroup _effectGroup;

    public MoviesEffectGroupTests()
    {
        _effectGroup = new MoviesEffectGroup(
            _moviesServiceMock.Object,
            _loggerMock.Object,
            _snackbarMock.Object);
        _effectGroup.SetDispatcher(_dispatcherMock.Object);
    }

    [Fact]
    public void CanHandle_LoadMovies_Should_ReturnTrue()
    {
        // Arrange
        LoadMovies action = new();

        // Act
        bool result = _effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_SearchMovies_Should_ReturnTrue()
    {
        // Arrange
        SearchMovies action = new("test");

        // Act
        bool result = _effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_LoadMoviesSuccess_Should_ReturnTrue()
    {
        // Arrange
        LoadMoviesSuccess action = new([], 0);

        // Act
        bool result = _effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_LoadMoviesFailure_Should_ReturnTrue()
    {
        // Arrange
        LoadMoviesFailure action = new("error");

        // Act
        bool result = _effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleLoadMoviesAsync_Success_Should_DispatchLoadMoviesSuccess()
    {
        // Arrange
        LoadMovies action = new();
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1", 2023),
            CreateTestMovie(2, "Movie 2", "Director 2", 2024)
        ];
        GetMoviesResponse response = new(movies, 10);
        MoviesState moviesState = new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination { CurrentPage = 1, TotalPages = 2, TotalItems = 10 }
        };

        _rootStateMock.Setup(x => x.GetSliceState<MoviesState>()).Returns(moviesState);
        _moviesServiceMock.Setup(x => x.GetMoviesAsync(1, 5, default)).ReturnsAsync(response);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<LoadMoviesSuccess>(a =>
                    a.Movies.Count == 2 && a.TotalItems == 10)),
            Times.Once);
    }

    [Fact]
    public async Task HandleLoadMoviesAsync_Failure_Should_DispatchLoadMoviesFailure()
    {
        // Arrange
        LoadMovies action = new();
        Exception exception = new("Service error");
        MoviesState moviesState = new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination { CurrentPage = 1, TotalPages = 1, TotalItems = 0 }
        };

        _rootStateMock.Setup(x => x.GetSliceState<MoviesState>()).Returns(moviesState);
        _moviesServiceMock.Setup(x => x.GetMoviesAsync(1, 5, default)).ThrowsAsync(exception);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<LoadMoviesFailure>(a => a.ErrorMessage == "Service error")),
            Times.Once);
    }

    [Fact]
    public async Task HandleSearchMoviesAsync_WithQuery_Should_FilterResults()
    {
        // Arrange
        SearchMovies action = new("Movie 1");
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1", 2023),
            CreateTestMovie(2, "Movie 2", "Director 2", 2024),
            CreateTestMovie(3, "Another Film", "Movie 1 Director", 2025)
        ];
        GetMoviesResponse response = new(movies, 3);
        MoviesState moviesState = new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination { CurrentPage = 1, TotalPages = 1, TotalItems = 3 }
        };

        _rootStateMock.Setup(x => x.GetSliceState<MoviesState>()).Returns(moviesState);
        _moviesServiceMock.Setup(x => x.GetMoviesAsync(1, 5, default)).ReturnsAsync(response);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(It.Is<LoadMoviesSuccess>(a =>
                a.Movies.Count == 2
                && a.Movies.Any(movie => movie.Title == "Movie 1")
                && a.Movies.Any(movie => movie.Director == "Movie 1 Director")
                && a.TotalItems == 2)),
            Times.Once);
    }

    [Fact]
    public async Task HandleSearchMoviesAsync_EmptyQuery_Should_ReturnAllMovies()
    {
        // Arrange
        SearchMovies action = new(string.Empty);
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1", 2023),
            CreateTestMovie(2, "Movie 2", "Director 2", 2024)
        ];
        GetMoviesResponse response = new(movies, 2);
        MoviesState moviesState = new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination { CurrentPage = 1, TotalPages = 1, TotalItems = 2 }
        };

        _rootStateMock.Setup(x => x.GetSliceState<MoviesState>()).Returns(moviesState);
        _moviesServiceMock.Setup(x => x.GetMoviesAsync(1, 5, default)).ReturnsAsync(response);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<LoadMoviesSuccess>(a =>
                    a.Movies.Count == 2 && a.TotalItems == 2)),
            Times.Once);
    }

    [Fact]
    public async Task HandleLoadMoviesSuccessAsync_Should_ShowSuccessNotification()
    {
        // Arrange
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1", 2023),
            CreateTestMovie(2, "Movie 2", "Director 2", 2024)
        ];
        LoadMoviesSuccess action = new(movies, 10);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _snackbarMock.Verify(
            x => x.Add(
                "Loaded 2 movies from the server.",
                Severity.Success,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.Once);

        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<AddNotification>(a =>
                    a.Notification is SuccessNotification && a.Notification.Message == "Loaded 2 movies from the server.")),
            Times.Once);
    }

    [Fact]
    public async Task HandleLoadMoviesFailureAsync_Should_ShowErrorNotification()
    {
        // Arrange
        LoadMoviesFailure action = new("Failed to load movies");

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _snackbarMock.Verify(
            x => x.Add(
                "Failed to load movies",
                Severity.Error,
                It.IsAny<Action<SnackbarOptions>>(),
                It.IsAny<string>()),
            Times.Once);

        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<AddNotification>(a =>
                    a.Notification is ErrorNotification && a.Notification.Message == "Failed to load movies")),
            Times.Once);
    }

    [Fact]
    public async Task SharedHelperMethodsAsync_Should_BeReusedAcrossHandlers()
    {
        // Arrange
        LoadMovies loadAction = new();
        SearchMovies searchAction = new("test");
        List<Movie> movies = [CreateTestMovie()];
        GetMoviesResponse response = new(movies, 1);
        MoviesState moviesState = new()
        {
            Movies = ImmutableDictionary<int, Movie>.Empty,
            IsLoading = false,
            ErrorMessage = null,
            Pagination = new Pagination { CurrentPage = 2, TotalPages = 1, TotalItems = 1 }
        };

        _rootStateMock.Setup(x => x.GetSliceState<MoviesState>()).Returns(moviesState);
        _moviesServiceMock.Setup(x => x.GetMoviesAsync(2, 5, default)).ReturnsAsync(response);

        // Act
        await _effectGroup.HandleAsync(loadAction, _rootStateMock.Object);
        await _effectGroup.HandleAsync(searchAction, _rootStateMock.Object);

        // Assert
        // Both handlers should use the same pagination info (page 2)
        _moviesServiceMock.Verify(x => x.GetMoviesAsync(2, 5, default), Times.Exactly(2));
    }

    private static Movie CreateTestMovie(
        int id = 1,
        string title = "Test Movie",
        string director = "Test Director",
        int year = 2023)
    {
        return new Movie
        {
            Id = id,
            Title = title,
            Year = year,
            Duration = "120 min",
            Rating = "PG-13",
            Imdb = 7.5,
            Metascore = 75,
            Description = "A test movie description",
            Director = director
        };
    }
}
