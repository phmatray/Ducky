// Effects and plain types for CtorCheckTests (SPEC §5.1, DUCKY309). Unregistered is never in any container.
#pragma warning disable CA1812 // justification: the fixtures are only used as type arguments or typeof operands
#pragma warning disable IDE0060, CS9113 // justification: the constructor parameters are what the check reads, never used
using Ducky.Tests.BuilderFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.CtorCheckFixtures;

internal sealed class Unregistered;

internal sealed class OtherUnregistered;

internal sealed record Ping;

internal abstract class PingEffect : Effect<Ping>
{
    public override Task Handle(Ping action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

// IStore, IDispatcher and slice types count as resolvable; Unregistered does not.
internal sealed class MissingServiceEffect(IStore store, IDispatcher dispatcher, CartSlice cart, Unregistered missing) : PingEffect;

// Every parameter resolves, or has a default value.
internal sealed class ResolvableEffect(IStore store, CartSlice cart, Marker marker, Unregistered? missing = null) : PingEffect;

internal sealed class ReplacedEffect(Unregistered missing) : PingEffect;

internal sealed class GithubEffect([FromKeyedServices("github")] Marker marker) : PingEffect;

internal sealed class KeyedPlusMissingEffect([FromKeyedServices("github")] Marker marker, Unregistered missing) : PingEffect;

internal sealed class MissingServiceMiddleware(CartSlice cart, Unregistered missing) : Middleware;

internal sealed class ResolvableMiddleware(CartSlice cart, Marker marker) : Middleware;

// Uses the store from its constructor (what DUCKY008 warns about): the store call re-enters materialization and throws.
// The thrown exception is kept, then rethrown, so the test can compare it with the one DUCKY353 wraps.
internal sealed class StoreUsingMiddleware : Middleware
{
    public StoreUsingMiddleware(IStore store, List<Exception> thrown)
    {
        try
        {
            store.Dispatch(new Ping());
        }
        catch (InvalidOperationException exception)
        {
            thrown.Add(exception);
            throw;
        }
    }
}

internal sealed class QueuedType(Unregistered missing);

internal sealed class ServiceKeyType([ServiceKey] string key);

// Two missing parameters: the first one is named.
internal sealed class TwoMissing(Unregistered first, OtherUnregistered second);

internal sealed class SecondCtorResolvable
{
    public SecondCtorResolvable(Unregistered missing)
    {
    }

    public SecondCtorResolvable(Marker marker)
    {
    }
}

internal sealed class MarkedCtorUnresolvable
{
    [ActivatorUtilitiesConstructor]
    public MarkedCtorUnresolvable(Unregistered missing)
    {
    }

    public MarkedCtorUnresolvable(Marker marker)
    {
    }
}

internal sealed class NoCtorResolvable
{
    public NoCtorResolvable(Unregistered missing)
    {
    }

    public NoCtorResolvable(Marker marker, Unregistered missing)
    {
    }
}

// No public constructor: no candidate, nothing to name. Materialization reports it (DUCKY353).
internal sealed class NoPublicCtor
{
    private NoPublicCtor()
    {
    }
}
