# DUCKY009: Select after the first render

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning at the `Select` call, in the `.razor` file
for markup and `@code` blocks (SPEC §11.2, §16.2, INV-20).

## What happened

A `DuckyComponent`, `DuckyComponent<TState>` or `DuckyLayout` calls `Select` where it runs after the component's first
render:

- in markup (`BuildRenderTree`), including inline handler lambdas and child content;
- in a templated delegate (`RenderFragment`, `RenderFragment<T>`, e.g. `RenderFragment Row => @<p>...</p>;` in
  `@code`), which runs when the component renders wherever it is declared;
- in an `OnParametersSet`, `OnParametersSetAsync`, `OnAfterRender` or `OnAfterRenderAsync` override;
- in an event handler: a method an `@onclick`, `@bind:after`, `@bind:set` or other `EventCallback` names, or calls
  from its lambda, generic handlers and child components' `EventCallback` parameters included.

Registration closes as the first render completes, so such a call throws `InvalidOperationException` (DUCKY352) at run
time. The analyzer also reads Razor's generated code, where markup lives, and reports at the `.razor` location.

Limit: handlers are matched to `Select` calls within one component type (its partial declarations included). A handler
inherited from a base component and named only in a derived component's markup is not reported; the call still throws
DUCKY352 at run time.

## How to fix it

Call `Select` in `OnInitialized` (or `OnInitializedAsync`) and keep the returned `Selection<T>` in a field; read its
`Value` in markup, lifecycle methods and handlers. A selector may read parameters and fields, because `Value` evaluates
on read, so a parameter change needs no new `Select`. `DuckyComponent<TState>.State` may be read anywhere.

