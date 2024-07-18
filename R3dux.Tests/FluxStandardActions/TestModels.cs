namespace R3dux.Tests.FluxStandardActions;

public sealed record TestCreateTodo : Fsa<TestCreateTodo.ActionPayload, TestCreateTodo.ActionMeta>
{
    public sealed record ActionPayload(string Title);
    public sealed record ActionMeta(DateTime TimeStamp);

    public override string TypeKey => "todos/create";

    public TestCreateTodo(string title)
        : base(new ActionPayload(title), new ActionMeta(DateTime.UtcNow)) { }
}

public sealed record TestToggleTodo : Fsa<TestToggleTodo.ActionPayload, TestToggleTodo.ActionMeta>
{
    public sealed record ActionPayload(Guid Id);
    public sealed record ActionMeta(DateTime TimeStamp);

    public override string TypeKey => "todos/toggle";

    public TestToggleTodo(Guid id)
        : base(new ActionPayload(id), new ActionMeta(DateTime.UtcNow)) { }
}

public sealed record TestDeleteTodo
    : Fsa<TestDeleteTodo.ActionPayload, TestDeleteTodo.ActionMeta>
{
    public sealed record ActionPayload(Guid Id);
    public sealed record ActionMeta(DateTime TimeStamp);

    public override string TypeKey => "todos/delete";

    public TestDeleteTodo(Guid id) 
        : base(new ActionPayload(id), new ActionMeta(DateTime.UtcNow)) { }
}

public sealed record TestFsaError(Exception Payload)
    : FsaError(Payload)
{
    public override string TypeKey => "error/action";
}

public sealed record TestFsaErrorWithMeta<TMeta>(Exception Payload, TMeta Meta)
    : FsaError<TMeta>(Payload, Meta)
{
    public override string TypeKey => "error/action";
}