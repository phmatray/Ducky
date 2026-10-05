# DUCKY352: Select after a component's first render

**Package:** Ducky.Blazor. **Surface:** `InvalidOperationException`, thrown synchronously by `Select` on a
`DuckyComponent`, `DuckyComponent<TState>` or `DuckyLayout`.

## What happened

`Select` was called after the component's first render, typically from markup (`BuildRenderTree`),
`OnParametersSet(Async)`, `OnAfterRender(Async)` or an event handler. A component registers its selections before it
first renders: registration closes as the first render completes, even when an `OnAfterRender` override does not call
`base`.

## How to fix it

Call `Select` in `OnInitialized` and keep the returned `Selection<T>` in a field; read its `Value` in markup. A
selector may read parameters and fields, because `Value` evaluates on read, so a parameter change needs no new
`Select`. `DuckyComponent<TState>.State` may be read anywhere, including conditionally after the first render. The
analyzer DUCKY009 reports a `Select` in markup at compile time.

Every Ducky error follows the same template: what happened, the concrete types, the exact fix, and a link to this
page.
