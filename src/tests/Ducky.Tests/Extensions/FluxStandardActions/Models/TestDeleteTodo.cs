// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Extensions.FluxStandardActions.Models;

public sealed record TestDeleteTodo
    : Fsa<TestDeleteTodo.ActionPayload, TestDeleteTodo.ActionMeta>
{
    public TestDeleteTodo(Guid id)
        : base(new ActionPayload(id), new ActionMeta(DateTime.UtcNow))
    {
    }

    public override string TypeKey => "todos/delete";

    public sealed record ActionPayload(Guid Id);

    public sealed record ActionMeta(DateTime TimeStamp);
}
