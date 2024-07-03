namespace Demo.AppStore;


public class MoviesService
{
    public async ValueTask<ImmutableArray<Movie>> GetMoviesAsync(CancellationToken ct = default)
    {
        await Task.Delay(1000, ct);

        // 1/3 chance of failing
        if (Random.Shared.Next(3) == 0)
        {
            throw new Exception("Failed to load movies");
        }
        
        return MoviesExamples.Movies;
    }
}