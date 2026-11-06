# Effects and concurrency policies

Reducers are pure, so anything that leaves the store (an HTTP call, a timer, storage) runs in an effect. An effect reacts to an action after it was reduced, and dispatches new actions with what it learned.

## An effect

The state and actions of a weather panel:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EffectsGuideTests.cs#effect-state)]

The effect injects the API and loads a forecast for each `LoadForecast`:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EffectsGuideTests.cs#effect-load-forecast)]

`context.Run` dispatches `started`, awaits `work`, then dispatches `succeeded(result)`. Any exception other than this run's own cancellation dispatches `failed(exception)`, an `HttpClient` timeout included. When the run is cancelled, nothing is dispatched.

Effects are created by the store, with their constructor dependencies from DI, and disposed with it. Register them next to the slices (`AddDuckyGenerated_{Assembly}()` does both; this sample nests the effect in a class named `App`):

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EffectsGuideTests.cs#effect-register)]

- Dispatch from an effect through its `context` (`context.Dispatch`, `context.DispatchAsync` or a generated helper on `context`), never through an injected `IStore`: the context knows which run is dispatching, so a superseded run's results are dropped (DUCKY008 warns on injecting `IStore` or `IDispatcher`).
- `context.State` is the latest snapshot on every read.
- Honour the `cancellationToken`: it is cancelled when the run is superseded or the store is disposed.
- A handler that throws dispatches `EffectFailed`; the store keeps running.
- `await store.WhenIdleAsync()` waits until the queue is empty and no effect runs. Set `LongRunning => true` on effects that never finish (pollers, listeners), so they don't count.
- An effect that dispatches its own trigger after an `await` (a poller: `Tick` → `await Task.Delay(interval, context.Time, cancellationToken)` → `context.Dispatch(new Tick())`) is not stopped by the depth guard, which bounds only synchronous chains (`MaxDispatchDepth`, 64). Such a loop runs until you stop it: give it `Switch`, honour the run's `cancellationToken`, and mark it `LongRunning` so `WhenIdleAsync` doesn't wait for it. Under `Switch` the poller's own `Tick` supersedes the run that dispatched it, and an extra `Tick` from elsewhere restarts the wait instead of starting a second loop, as `Merge` would. Not `Exhaust`: the run still holds the slot when it dispatches its `Tick`, so that `Tick` is dropped and the poller stops after one round.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EffectsGuideTests.cs#effect-poller)]

## Concurrency policies

`Policy` decides what happens when the action arrives while an earlier run is still going. The default is `Merge`.

| Policy | A new action while a run is going |
|---|---|
| `Merge` | starts another run; both go on |
| `Switch` | cancels the running one and starts the new one: the newest wins (search boxes, "load the selected item") |
| `Exhaust` | is ignored until the running one finishes (submit buttons, "refresh") |
| `Queue` | waits for the running one, then starts: one at a time, in order |

The policy applies per concurrency key. Without `ConcurrencyKey`, all runs of the effect share one slot. `LoadForecastEffect` keys by city, so a second `LoadForecast("Paris")` cancels the first, while `LoadForecast("Rome")` runs alongside. A key needs value equality, such as a string, a number or a record (DUCKY013).

## Several actions in one class

An `EffectGroup` registers one handler per action type in its constructor, each with its own policy and key:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EffectsGuideTests.cs#effect-group)]

[Testing with Ducky.Testing](testing.md) shows how to test an effect with fakes for its dependencies.
