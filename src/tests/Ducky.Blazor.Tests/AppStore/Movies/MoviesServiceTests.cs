// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Movies;

[SuppressMessage("Roslynator", "RCS1046:Asynchronous method name should end with \'Async\'")]
public class MoviesServiceTests
{
    private readonly MoviesService _moviesService = new();

    [Fact]
    public async Task GetMoviesAsync_ShouldReturnMovies_WhenPageExists()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 2;

        // Act
        GetMoviesResponse result = await _moviesService
            .GetMoviesAsync(pageNumber, pageSize, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        // Assert
        result.Movies.Length.ShouldBe(pageSize);
        result.Movies.ShouldBe(MoviesExamples.Movies.Take(pageSize));
    }

    [Fact]
    public async Task GetMoviesAsync_ShouldReturnEmpty_WhenPageDoesNotExist()
    {
        // Arrange
        const int pageNumber = 100; // Assuming there are less than 100 pages
        const int pageSize = 2;

        // Act
        GetMoviesResponse result = await _moviesService
            .GetMoviesAsync(pageNumber, pageSize, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        // Assert
        result.Movies.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMoviesAsync_ShouldHandleCancellation()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 2;

        // Act
        Func<Task> act = async () =>
        {
            using CancellationTokenSource cts = new();
            await cts.CancelAsync().ConfigureAwait(false);
            await _moviesService.GetMoviesAsync(pageNumber, pageSize, cts.Token).ConfigureAwait(false);
        };

        // Assert
        await act.ShouldThrowAsync<TaskCanceledException>().ConfigureAwait(true);
    }
}
