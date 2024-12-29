// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
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
        GetMoviesResponse result = await _moviesService.GetMoviesAsync(pageNumber, pageSize).ConfigureAwait(true);

        // Assert
        result.Movies.Should().HaveCount(pageSize);
        result.Movies.Should().BeEquivalentTo(MoviesExamples.Movies.Take(pageSize));
    }

    [Fact]
    public async Task GetMoviesAsync_ShouldReturnEmpty_WhenPageDoesNotExist()
    {
        // Arrange
        const int pageNumber = 100; // Assuming there are less than 100 pages
        const int pageSize = 2;

        // Act
        GetMoviesResponse result = await _moviesService.GetMoviesAsync(pageNumber, pageSize).ConfigureAwait(true);

        // Assert
        result.Movies.Should().BeEmpty();
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
        await act.Should().ThrowAsync<TaskCanceledException>().ConfigureAwait(true);
    }
}
