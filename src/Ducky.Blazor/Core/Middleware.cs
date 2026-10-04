namespace Ducky.Blazor;

// Placeholders whose positions AddBlazor fixes (SPEC §11.1): inert until the prerender handoff (§11.4) and the
// persistence middleware (§11.5) fill them.
internal sealed class PrerenderHandoff : Middleware;

internal sealed class PersistenceMiddleware : Middleware;
