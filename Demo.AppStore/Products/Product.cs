namespace Demo.AppStore;

public record Product(
    Guid Id,
    string Name,
    decimal Price, 
    string Category)
    : IEntity<Guid>;