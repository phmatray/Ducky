// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Movies;

public sealed class MoviesEffectsTests : IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<IAction> _actionsSubject = new();
    private readonly Subject<RootState> _rootStateSubject = new();
    private readonly List<IAction> _actualActions = [];
    private readonly Mock<IMoviesService> _moviesServiceMock = new();

    public MoviesEffectsTests()
    {
        new LoadMoviesEffect(_moviesServiceMock.Object)
            .Handle(_actionsSubject, _rootStateSubject)
            .Subscribe(action => _actualActions.Add(action))
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _actionsSubject.Dispose();
        _rootStateSubject.Dispose();
    }

    [Fact]
    public async Task LoadMoviesEffect_ShouldEmitLoadMoviesSuccess_WhenServiceCallSucceeds()
    {
        // Arrange
        SetupRootState();
        List<Movie> movies = [MoviesExamples.Movies[0], MoviesExamples.Movies[1]];
        var immutableMovies = ImmutableArray.CreateRange(movies);
        _moviesServiceMock
            .Setup(service => service.GetMoviesAsync(It.IsAny<int>(), It.IsAny<int>(), CancellationToken.None))
            .ReturnsAsync(new GetMoviesResponse(immutableMovies, movies.Count));

        // Act
        _actionsSubject.OnNext(new LoadMovies(1, 10));
        await Task.Delay(100); // Ensure the async call completes

        // Assert
        _actualActions.Should().HaveCount(1);
        _actualActions[0].Should().BeOfType<LoadMoviesSuccess>()
            .Which.Movies.Should().BeEquivalentTo(movies);
    }

    [Fact]
    public async Task LoadMoviesEffect_ShouldEmitLoadMoviesFailure_WhenServiceCallFails()
    {
        // Arrange
        SetupRootState();
        const string exceptionMessage = "Service call failed";
        _moviesServiceMock
            .Setup(service => service.GetMoviesAsync(It.IsAny<int>(), It.IsAny<int>(), CancellationToken.None))
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        _actionsSubject.OnNext(new LoadMovies(1, 10));
        await Task.Delay(100); // Ensure the async call completes

        // Assert
        _actualActions.Should().HaveCount(1); // One for failure
        _actualActions[0].Should().BeOfType<LoadMoviesFailure>()
            .Which.Error.Message.Should().Be(exceptionMessage);
    }

    private void SetupRootState()
    {
        var dictionary = new Dictionary<string, object>
        {
            { "movies", new List<Movie>() }
        };

        var rootState = new RootState(dictionary.ToImmutableSortedDictionary());
        _rootStateSubject.OnNext(rootState);
    }
}
