namespace TheBlazorState.Storage;

public record StorageMetadata(string Key, TimeSpan? TimeToLive, DateTimeOffset Timestamp);
