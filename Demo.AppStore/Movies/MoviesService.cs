namespace Demo.AppStore;

public class MoviesService
{
    private static readonly ImmutableArray<Movie> Movies =
    [
        new Movie("The Shawshank Redemption", "Frank Darabont", 1994),
        new Movie("The Godfather", "Francis Ford Coppola", 1972),
        new Movie("The Dark Knight", "Christopher Nolan", 2008),
        new Movie("Inception", "Christopher Nolan", 2010),
        new Movie("Pulp Fiction", "Quentin Tarantino", 1994),
        new Movie("The Lord of the Rings: The Return of the King", "Peter Jackson", 2003)
    ];
    
    public async Task<ImmutableArray<Movie>> GetMoviesAsync()
    {
        await Task.Delay(1000);
        return Movies;
    }
}