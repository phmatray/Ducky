// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.ConsoleApp.Todos;

[DuckyAction]
public sealed record AddTodo(string Title, string? Id = null);

[DuckyAction]
public sealed record ToggleTodo(string Id);

[DuckyAction]
public sealed record RemoveTodo(string Id);

[DuckyAction]
public sealed record ClearCompleted;

[DuckyAction]
public sealed record ToggleAll(bool IsCompleted);
