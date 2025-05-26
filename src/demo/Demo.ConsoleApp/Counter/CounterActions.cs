namespace Demo.ConsoleApp.Counter;

[DuckyAction]
public sealed record Increment(int Amount = 1);

[DuckyAction]
public sealed record Decrement(int Amount = 1);

[DuckyAction]
public sealed record Reset;

[DuckyAction]
public sealed record SetValue(int Value);

[DuckyAction]
public sealed record IncrementAsync(int Amount = 1, int DelayMs = 1000);
