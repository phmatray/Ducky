namespace Demo.AppStore.Tests.Movies;

public class MoviesServiceTests
{
    private readonly IMoviesService _moviesService = new MoviesService();

    [Fact]
    public async Task GetMoviesAsync_ShouldReturnMovies_WhenPageExists()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 2;

        // Act
        var result = await _moviesService.GetMoviesAsync(pageNumber, pageSize);

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
        var result = await _moviesService.GetMoviesAsync(pageNumber, pageSize);

        // Assert
        result.Movies.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMoviesAsync_ShouldHandleCancellation()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 2;
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await _moviesService.GetMoviesAsync(pageNumber, pageSize, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task GetMoviesAsync_ShouldThrowException_WhenRandomFails()
    {
        // This test needs to control the random aspect, which is not directly testable.
        // For this, you would typically use dependency injection or another method to control the random behavior.
        // For simplicity, we'll just run it multiple times to catch the failure randomly.

        // Arrange
        const int pageNumber = 1;
        const int pageSize = 2;
        var exceptionsThrown = false;

        for (int i = 0; i < 100; i++)
        {
            try
            {
                await _moviesService.GetMoviesAsync(pageNumber, pageSize);
            }
            catch (Exception ex) when (ex.Message == "Failed to load movies")
            {
                exceptionsThrown = true;
                break;
            }
        }

        // Assert
        exceptionsThrown.Should().BeTrue();
    }
}