# Union effects and dispatch helpers

When a feature's actions form a C# union, one effect group can handle all of them through one `switch`, and one `[DuckyAction]` gives every case its dispatch helper. Both are generated from the union's cases, so a case added to the union gets its helper and its registration at the next build.

## Dispatch helpers for every case

Put `[DuckyAction]` on the union, not on each case:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionEffectsGuideTests.cs#union-effects-actions)]

The generator emits the helpers of each case, named after the case (`store.AddNote("milk")`, `store.AddNoteAsync("milk")`), as if each case carried `[DuckyAction]` itself. A helper dispatches the case, which is what dispatching the union delivers: dispatch unwraps a union to its case. A case that is itself a union is unwrapped to its own cases.

- `MethodName` can't name several cases, so on a union it is ignored ([DUCKY028](../../diagnostics/DUCKY028.md)). To rename one case's helpers, put `[DuckyAction(MethodName = "...")]` on that case: a case with its own `[DuckyAction]` keeps its own helpers.
- Only the cases declared in your project get helpers: a case such as `string` gets none, and a case shared by two `[DuckyAction]` unions gets one set.
- A case that can't get a helper (an abstract record, for example) is skipped; dispatch it through the union instead.

## One effect group for the union

Derive from `EffectGroup<TAction>` with the union as `TAction`, and declare the group `partial`:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionEffectsGuideTests.cs#union-effects-group)]

The generator registers each case for `Handle`, wrapped in the union, so a case dispatched alone or inside the union runs the same arm. A case missing from the `switch` is a CS8509 warning, an error with warnings as errors.

- The group, and every type it is nested in, must be `partial` ([DUCKY027](../../diagnostics/DUCKY027.md)), and `TAction` must be a union ([DUCKY026](../../diagnostics/DUCKY026.md)).
- Each case runs as its own effect with the `Merge` policy and one global slot. For a policy or a key per action, derive from `EffectGroup` and register each action with `On<TAction>(handler, policy, key)`, as in [Effects and concurrency policies](effects.md).
- Without the generator (`DuckyUseGenerators` off), override `RegisterCases` and call `On<Case>((a, c, t) => Handle(new TAction(a), c, t))` for each case.

## One slot for every case

An effect group gives each case its own slot, so one case never cancels the run of another. When several actions start the same work and a new one must replace the run in flight, they need one slot. Without a union effect, that takes a forwarder: every trigger dispatches one internal action, and one effect handles it.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionEffectsGuideTests.cs#union-effect-before)]

Nothing checks that the group lists every trigger, each load appears twice in the action log, and cancelling needs a flag of its own.

Derive from `Effect<TAction>` with the union as `TAction` instead, and declare the effect `partial`:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionEffectsGuideTests.cs#union-effect-slot)]

The generator registers each case for `Handle`, wrapped in the union. One effect serves every case, so its `Policy` and `ConcurrencyKey` apply across the cases:

- `Concurrency.Switch`: the newest case wins. It cancels the run in flight, and anything that run dispatches afterwards is dropped.
- `Concurrency.Exhaust`: the first case holds the slot, and the other cases are dropped until its run ends.
- `ConcurrencyKey(TAction)` receives the union. Runs with an equal key share a slot, and runs with different keys run side by side.

A case missing from the `switch` is a CS8509 warning, an error with warnings as errors.

- The effect, and every type it is nested in, must be `partial` ([DUCKY027](../../diagnostics/DUCKY027.md)). A non-union `Effect<TAction>` is an ordinary effect that the union rules do not apply to.
- A case that belongs to two trigger unions starts both effects, each in its own slots.
- Without the generator (`DuckyUseGenerators` off), override `RegisterCases` and call `OnCase<Case>(a => new TAction(a))` for each case.

Choose `Effect<TAction>` over a union when the cases start or stop the same work. Choose `EffectGroup<TAction>` when the cases are independent: each case stays its own effect, with its own slot.

## Testing it

Register the group on a `TestStore`, dispatch through the generated helpers, and read what the group dispatched:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionEffectsGuideTests.cs#union-effects-test)]
