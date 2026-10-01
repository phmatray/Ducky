# DUCKY353: Effect or middleware constructor threw

**Package:** Ducky. **Surface:** `DuckyConfigurationException` from the first store use, rethrown by every later call until disposal.

## What happened

An effect, middleware or reactive-effect constructor threw when the store created them on its first use. The
exception wraps the original one as its `InnerException`, and names DUCKY307 or DUCKY308 when the constructor was an
`EffectGroup` with a non-concrete or duplicate `On<T>()`. The instances created before the throw are disposed first,
and every later call before disposal rethrows the same exception.

## How to fix it

Fix the constructor so that it does not throw; the inner exception has the details. Keep constructors free of I/O and
side effects: start work from a `StoreInitialized` effect instead.

Every Ducky configuration error follows the same template: what happened, the concrete types and keys, the exact fix,
and a link to this page. Configuration errors found at first store resolution are reported together in one
`DuckyConfigurationException`, whose `Errors` list holds one `DuckyError` per problem.
