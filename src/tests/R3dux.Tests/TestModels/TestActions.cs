namespace R3dux.Tests.TestModels;

internal record TestAction(string Name) : IAction;

internal record IntegerAction(int Value) : IAction;
