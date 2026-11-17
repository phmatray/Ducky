using Spike.Lib;

Console.WriteLine(Spike.Probe.Report);
Console.WriteLine($"runtime: {new CartAction(new AddItem(1)).Value} / {new RefAction(new RefB("x")).Value}");

public sealed record AddItem(int Id);
public sealed record RemoveItem(int Id);
public sealed record Clear;

public union CartAction(AddItem, RemoveItem, Clear);
