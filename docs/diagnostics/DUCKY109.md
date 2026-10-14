# DUCKY109: result of a draft member's method discarded

**Package:** Ducky.Draft (analyzer `Ducky.Draft.Generators`). **Surface:** build warning on the call (SPEC §13.3, §16.2).

## What happened

Inside a `Produce` recipe, a statement calls a method on a plain get/set member of a draft and throws the result away,
when the method returns a value of the member's own type: `d.Pending.Enqueue(job);` on an `ImmutableQueue<T>`,
`d.Index.Add(key, value);` on an `ImmutableSortedDictionary<TKey, TValue>`, `d.Todos.Upsert(todo);` on an
`EntityState<TKey, TEntity>`, `d.Title.Trim();` on a `string`. Such a member is immutable: the method returns a new
instance and leaves the member as it was, so the edit is lost and `Produce` returns the base (INV-26). Mutty mapped
`ImmutableQueue`, `ImmutableStack` and `ImmutableSortedDictionary` to mutable `Queue`, `Stack` and `SortedDictionary`,
so a migrated recipe that calls them in place compiles and silently does nothing.

The member can belong to the recipe's draft, a nested draft (`d.Child.Pending`), an element draft (`d.Items[0].Pending`)
or another draft, and the receiver can be an element of a plain collection draft (`d.Queues[0].Enqueue(job);`). Calls in
lambdas and local functions inside the recipe, and recipes in generated code (a Razor component's `@code` block), are
analyzed too.

Not reported: an assigned result (`d.Pending = d.Pending.Enqueue(job);`), an explicit discard (`_ = d.Pending.Enqueue(job);`),
a method that returns another type (`d.Index.ContainsKey(key);`), a get-only draft member (it can't be assigned), a member of a draft type itself (a nested draft or a
collection draft, edited in place), and a call outside a `Produce` recipe (a `CreateDraft()` draft edited step by step).
The analyzer looks at the member itself: a local copy (`var q = d.Pending; q.Enqueue(job);`), an extension method and a
conditional access (`d.Pending?.Enqueue(job);`) are not followed.

## How to fix it

Assign the result back to the member: `d.Pending = d.Pending.Enqueue(job);`. For a member edited often, consider a type
the draft maps to a collection draft (`ImmutableList<T>`, `ImmutableArray<T>`, `ImmutableDictionary<TKey, TValue>`,
`ImmutableHashSet<T>`, `ImmutableSortedSet<T>`), whose draft is edited in place (`d.Items.Add(x)`).
