namespace Demo.AppStore;

public record Movie
{
    public required string Title { get; init; }
    public required int Year { get; init; }
    public required string Duration { get; init; }
    public required string Rating { get; init; }
    public required double Imdb { get; init; }
    public required int Metascore { get; init; }
    public required string Description { get; init; }
    public required string Director { get; init; }
    public ImmutableArray<string> Actors { get; init; } = [];
}
