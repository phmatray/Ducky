# DUCKY106: draft escapes its `Produce` recipe

**Package:** Ducky.Draft (analyzer `Ducky.Draft.Generators`). **Surface:** build warning on the assignment
(SPEC §13.3, §16.2).

## What happened

Inside a `Produce` recipe, a draft reached from the recipe's parameter (the draft itself, a nested draft such as
`d.Child`, a collection draft such as `d.Items`, an element draft such as `d.Items[0]`, a local or `foreach` variable
the recipe set from one, or the result of a local function that returns one) is stored where it outlives the recipe:
assigned to a field, an array element or a property of an object the recipe did not create (a field of the
component, a parameter, a local the recipe also set from elsewhere or passed by `ref` or `out`, a local bound by
`out var`, a pattern or a deconstruction, a `foreach` variable), or to a variable captured from outside the lambda. A lambda or a local function that reads the draft escapes the same way when it is
stored there (`_later = () => d.Title = "x"`). `Produce` revokes the draft and every draft below it when it returns, so
any later read or write through the stored reference throws `ObjectDisposedException` ("Draft used after Produce
returned.") instead of editing anything (INV-26).

What stays inside the recipe is not reported: assigning a draft to another member of the same draft
(`d.Child = d.Child`), to a local of the recipe, to a member or element of an object, array or collection the recipe
created (`var h = new Holder { Draft = d };`, `var a = new Todo.Draft[1]; a[0] = d;`), storing a draft that a key read
from the draft only picks (`_saved = drafts[d.Title]` over a recipe-local map of `CreateDraft()` drafts), or calling a
helper inline (`Child.Draft Find(int id) => d.Items.First(x => x.Id == id); Find(3).Value = 1;`). Recipes in generated code, such as
a Razor component's `@code` block, are analyzed too. The analyzer sees assignments only: storing an object the recipe created after
putting a draft in it (`var h = new Holder { Draft = d }; _holder = h;`), passing a draft to a method that keeps it
(`list.Add(d)`), deconstructing it into a field, binding it with a pattern (`d.Child is var c`) or
subscribing a lambda that reads it to an event is not reported.

## How to fix it

Make every edit inside the recipe, and keep only its result: `var next = state.Produce(d => d.Child.Value = 2);`. For
an edit spread over several steps, create the draft explicitly with `CreateDraft()`, which is never revoked, and call
`Build()` when done.
