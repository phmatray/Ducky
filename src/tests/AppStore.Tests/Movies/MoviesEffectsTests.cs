// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Movies;

[SuppressMessage("Roslynator", "RCS1046:Asynchronous method name should end with \'Async\'")]
public sealed class MoviesEffectsTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<object> _actionsSubject = new();
    private readonly Subject<IRootState> _rootStateSubject = new();
    private readonly List<object> _actualActions = [];
    private readonly Mock<IMoviesService> _moviesServiceMock = new();

    public MoviesEffectsTests()
    {
        ObservableSystem.DefaultTimeProvider = _timeProvider;

        new LoadMoviesEffect(_moviesServiceMock.Object)
            .Handle(_actionsSubject, _rootStateSubject)
            .Subscribe(_actualActions.Add)
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _actionsSubject.Dispose();
        _rootStateSubject.Dispose();
    }

    [Fact]
    public void LoadMoviesEffect_ShouldEmitLoadMoviesSuccess_WhenServiceCallSucceeds()
    {
        // Arrange
        SetupRootState();
        List<Movie> movies = [MoviesExamples.Movies[0], MoviesExamples.Movies[1]];
        ValueCollection<Movie> immutableMovies = [..movies];
        _moviesServiceMock
            .Setup(service => service.GetMoviesAsync(It.IsAny<int>(), It.IsAny<int>(), CancellationToken.None))
            .ReturnsAsync(new GetMoviesResponse(immutableMovies, movies.Count));

        // Act
        _actionsSubject.OnNext(new LoadMovies());
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Assert
        _actualActions.Count.ShouldBe(1);
        _actualActions[0].ShouldBeOfType<LoadMoviesSuccess>().Movies.ShouldBeEquivalentTo(immutableMovies);
    }

    [Fact]
    public void LoadMoviesEffect_ShouldEmitLoadMoviesFailure_WhenServiceCallFails()
    {
        // Arrange
        SetupRootState();
        const string exceptionMessage = "Service call failed";
        _moviesServiceMock
            .Setup(service => service.GetMoviesAsync(It.IsAny<int>(), It.IsAny<int>(), CancellationToken.None))
            .ThrowsAsync(new MovieException(exceptionMessage));

        // Act
        _actionsSubject.OnNext(new LoadMovies());
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Assert
        _actualActions.ShouldHaveSingleItem(); // One for failure
        _actualActions[0].ShouldBeOfType<LoadMoviesFailure>().Error.Message.ShouldBe(exceptionMessage);
    }

    private void SetupRootState()
    {
        ImmutableSortedDictionary<string, object> dictionary = ImmutableSortedDictionary
            .Create<string, object>()
            .Add(
                "movies",
                new MoviesState
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
                });

        RootState rootState = new(dictionary);
        _rootStateSubject.OnNext(rootState);
    }
}
