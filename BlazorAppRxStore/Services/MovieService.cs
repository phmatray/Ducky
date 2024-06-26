namespace BlazorAppRxStore.Services;

public class MovieService
{
    private static readonly List<Movie> Movies =
    [
        new Movie("Back to the Future", 1985),
        new Movie("The Terminator", 1984),
        new Movie("Ghostbusters", 1984),
        new Movie("E.T. the Extra-Terrestrial", 1982),
        new Movie("Raiders of the Lost Ark", 1981)
    ];

    public async Task<IImmutableList<Movie>> GetMoviesAsync()
    {
        await Task.Delay(1000);
        return Movies.ToImmutableList();
    }
}