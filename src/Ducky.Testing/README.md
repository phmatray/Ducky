# Ducky.Testing

Ducky.Testing runs your [Ducky](https://www.nuget.org/packages/Ducky) slices and effects on a real store with a fake clock, and works with any test framework: a failed check throws `DuckyAssertionException`.

- `SliceTest` seeds a state, dispatches actions and checks the result.
- `EffectTest` runs one effect for one action and returns what it dispatched.
- `TestStore` is a whole store for a test: seed slices, dispatch, wait until it settles, advance its `FakeTimeProvider`.

## Install

```shell
dotnet add package Ducky.Testing --prerelease
```

## Example

Given this slice:

```csharp
using Ducky;

public sealed record Counter(int Value);

public sealed record Increment(int By);

public sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>((state, action) => state with { Value = state.Value + action.By });

    protected override Counter Initial => new(0);
}
```

a test (xUnit here) checks its reducer:

```csharp
using Ducky.Testing;

public sealed class CounterSliceTests
{
    [Fact]
    public void Increment_AddsToValue() =>
        SliceTest.For<CounterSlice, Counter>()
            .Given(new Counter(1))
            .When(new Increment(2))
            .Then(new Counter(3));
}
```

`Then(expected)` compares with `Equals`, `Then(state => ...)` runs your own assertions, and `ThenUnchanged()` checks that the reducers returned the same reference.

## Learn more

- Guide: https://phmatray.github.io/Ducky/docs/guides/core/testing.html
- Documentation: https://phmatray.github.io/Ducky/
- Changelog: https://github.com/phmatray/Ducky/blob/v2/CHANGELOG.md
- Issues and source: https://github.com/phmatray/Ducky

Ducky.Testing is licensed under the Apache License 2.0.
