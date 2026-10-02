namespace Ducky;

// What the store builds once, on its first use (SPEC §6.6): its middleware in registration order, the effect index (exact
// action type -> effects, in registration order) and the effects it created, which it owns and disposes (§6.11 5b). An
// AddEffect(instance) instance runs but is not owned.
internal sealed class Materialized((Effect Effect, bool Owned)[] effects, Middleware[] middleware)
{
    public Middleware[] Middleware { get; } = middleware;

    public Effect[] OwnedEffects { get; } = [.. effects.Where(e => e.Owned).Select(e => e.Effect)];

    public Dictionary<Type, Effect[]> Effects { get; } = effects
        .GroupBy(e => e.Effect.ActionType, e => e.Effect)
        .ToDictionary(g => g.Key, g => g.ToArray());
}
