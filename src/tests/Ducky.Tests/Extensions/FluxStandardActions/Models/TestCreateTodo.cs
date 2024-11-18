// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Extensions.FluxStandardActions.Models;

public sealed record TestCreateTodo : Fsa<TestCreateTodo.ActionPayload, TestCreateTodo.ActionMeta>
{
    public TestCreateTodo(string title)
        : base(new ActionPayload(title), new ActionMeta(DateTime.UtcNow))
    {
    }

    public override string TypeKey => "todos/create";

    public sealed record ActionPayload(string Title);

    public sealed record ActionMeta(DateTime TimeStamp);
}
