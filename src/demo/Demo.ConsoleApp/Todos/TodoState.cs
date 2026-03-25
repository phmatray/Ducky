// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.ConsoleApp.Todos;

public sealed record TodoItem(string Id, string Title, bool IsCompleted = false) : IEntity<string>
{
    public string EntityId => Id;
}

public sealed record TodoState : NormalizedState<string, TodoItem, TodoState>
{
    public int CompletedCount => SelectEntities(t => t.IsCompleted).Length;
    public int ActiveCount => SelectEntities(t => !t.IsCompleted).Length;
}
