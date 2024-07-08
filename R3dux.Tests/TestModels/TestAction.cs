namespace R3dux.Tests.TestModels;

internal class TestAction(string name) : IAction
{
    private string Name { get; } = name;

    public override bool Equals(object? obj)
        => obj is TestAction action
           && Name == action.Name;

    public override int GetHashCode()
        => HashCode.Combine(Name);
}