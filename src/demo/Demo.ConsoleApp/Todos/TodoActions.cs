// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.ConsoleApp.Todos;

[DuckyAction]
public sealed partial record AddTodo(string Title, string? Id = null);

[DuckyAction]
public sealed partial record ToggleTodo(string Id);

[DuckyAction]
public sealed partial record RemoveTodo(string Id);

[DuckyAction]
public sealed partial record ClearCompleted;

[DuckyAction]
public sealed partial record ToggleAll(bool IsCompleted);
