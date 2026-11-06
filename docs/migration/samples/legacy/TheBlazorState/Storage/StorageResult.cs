namespace TheBlazorState.Storage;

public record StorageResult<T>(bool Found, T? Value, DateTimeOffset? PersistedAt);
