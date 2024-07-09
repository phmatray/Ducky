namespace Demo.AppStore;


public class MoviesService
{
    public async ValueTask<ImmutableArray<Movie>> GetMoviesAsync(
        int pageNumber = 1, 
        int pageSize = 5, 
        CancellationToken ct = default)
    {
        await Task.Delay(1000, ct);

        // 1/3 chance of failing
        if (Random.Shared.Next(3) == 0)
        {
            throw new Exception("Failed to load movies");
        }
        
        // Calculate the total number of movies and the start index for the requested page.
        var totalMovies = MoviesExamples.Movies.Length;
        var startIndex = (pageNumber - 1) * pageSize;

        // Ensure the start index is within the range of the total number of movies.
        if (startIndex >= totalMovies)
        {
            return ImmutableArray<Movie>.Empty;
        }

        // Calculate the number of movies to return on the current page.
        var moviesToTake = Math.Min(pageSize, totalMovies - startIndex);

        // Retrieve the paginated list of movies.
        var paginatedMovies = MoviesExamples.Movies
            .Skip(startIndex)
            .Take(moviesToTake)
            .ToImmutableArray();

        return paginatedMovies;
    }
}