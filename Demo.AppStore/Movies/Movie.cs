namespace Demo.AppStore;

public record Movie
{
    public string Title { get; init; }
    public int Year { get; init; }
    public string Duration { get; init; }
    public string Rating { get; init; }
    public double Imdb { get; init; }
    public int Metascore { get; init; }
    public string Description { get; init; }
    public string Director { get; init; }
    public ImmutableArray<string> Actors { get; init; } = [];
}
