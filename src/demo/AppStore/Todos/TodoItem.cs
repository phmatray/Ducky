// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore;

public class TodoItem(Guid id, string title, bool isCompleted = false)
    : IEntity<Guid>
{
    public TodoItem(string title, bool isCompleted = false)
        : this(Guid.NewGuid(), title, isCompleted)
    {
    }

    public Guid Id { get; } = id;

    public string Title { get; set; } = title;

    public bool IsCompleted { get; set; } = isCompleted;
}
