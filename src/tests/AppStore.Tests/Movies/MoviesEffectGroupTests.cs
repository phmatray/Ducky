using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace AppStore.Tests.Movies;

public class MoviesEffectGroupTests
{
    private readonly IMoviesService _moviesService = A.Fake<IMoviesService>();
    private readonly ILogger<MoviesEffectGroup> _logger = A.Fake<ILogger<MoviesEffectGroup>>();
    private readonly ISnackbar _snackbar = A.Fake<ISnackbar>();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly IStateProvider _stateProvider = A.Fake<IStateProvider>();
    private readonly MoviesEffectGroup _effectGroup;

    public MoviesEffectGroupTests()
    {
        _effectGroup = new MoviesEffectGroup(
            _moviesService,
            _logger,
            _snackbar);
        _effectGroup.SetDispatcher(_dispatcher);
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
            CreateTestMovie(1, "Movie 1", "Director 1"),
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

        A.CallTo(() => _stateProvider.GetSlice<MoviesState>()).Returns(moviesState);
        A.CallTo(() => _moviesService.GetMoviesAsync(1, 5, A<CancellationToken>.Ignored)).Returns(response);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<LoadMoviesSuccess>.That.Matches(a =>
                a.Movies.Count == 2 && a.TotalItems == 10)))
            .MustHaveHappenedOnceExactly();
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

        A.CallTo(() => _stateProvider.GetSlice<MoviesState>()).Returns(moviesState);
        A.CallTo(() => _moviesService.GetMoviesAsync(1, 5, A<CancellationToken>.Ignored)).Throws(exception);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<LoadMoviesFailure>.That.Matches(a => a.ErrorMessage == "Service error")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleSearchMoviesAsync_WithQuery_Should_FilterResults()
    {
        // Arrange
        SearchMovies action = new("Movie 1");
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1"),
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

        A.CallTo(() => _stateProvider.GetSlice<MoviesState>()).Returns(moviesState);
        A.CallTo(() => _moviesService.GetMoviesAsync(1, 5, A<CancellationToken>.Ignored)).Returns(response);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<LoadMoviesSuccess>.That.Matches(a =>
                a.Movies.Count == 2
                && a.Movies.Any(movie => movie.Title == "Movie 1")
                && a.Movies.Any(movie => movie.Director == "Movie 1 Director")
                && a.TotalItems == 2)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleSearchMoviesAsync_EmptyQuery_Should_ReturnAllMovies()
    {
        // Arrange
        SearchMovies action = new(string.Empty);
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1"),
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

        A.CallTo(() => _stateProvider.GetSlice<MoviesState>()).Returns(moviesState);
        A.CallTo(() => _moviesService.GetMoviesAsync(1, 5, A<CancellationToken>.Ignored)).Returns(response);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<LoadMoviesSuccess>.That.Matches(a =>
                a.Movies.Count == 2 && a.TotalItems == 2)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleLoadMoviesSuccessAsync_Should_ShowSuccessNotification()
    {
        // Arrange
        List<Movie> movies =
        [
            CreateTestMovie(1, "Movie 1", "Director 1"),
            CreateTestMovie(2, "Movie 2", "Director 2", 2024)
        ];
        LoadMoviesSuccess action = new(movies, 10);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _snackbar.Add(
            "Loaded 2 movies from the server.",
            Severity.Success,
            A<Action<SnackbarOptions>>.Ignored,
            A<string>.Ignored))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _dispatcher.Dispatch(
            A<AddNotification>.That.Matches(a =>
                a.Notification is SuccessNotification && a.Notification.Message == "Loaded 2 movies from the server.")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleLoadMoviesFailureAsync_Should_ShowErrorNotification()
    {
        // Arrange
        LoadMoviesFailure action = new("Failed to load movies");

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _snackbar.Add(
            "Failed to load movies",
            Severity.Error,
            A<Action<SnackbarOptions>>.Ignored,
            A<string>.Ignored))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _dispatcher.Dispatch(
            A<AddNotification>.That.Matches(a =>
                a.Notification is ErrorNotification && a.Notification.Message == "Failed to load movies")))
            .MustHaveHappenedOnceExactly();
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

        A.CallTo(() => _stateProvider.GetSlice<MoviesState>()).Returns(moviesState);
        A.CallTo(() => _moviesService.GetMoviesAsync(2, 5, A<CancellationToken>.Ignored)).Returns(response);

        // Act
        await _effectGroup.HandleAsync(loadAction, _stateProvider);
        await _effectGroup.HandleAsync(searchAction, _stateProvider);

        // Assert
        // Both handlers should use the same pagination info (page 2)
        A.CallTo(() => _moviesService.GetMoviesAsync(2, 5, A<CancellationToken>.Ignored))
            .MustHaveHappenedTwiceExactly();
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
