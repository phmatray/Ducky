namespace Ducky.Blazor;

// A placeholder whose position AddBlazor fixes (SPEC §11.1), after the prerender handoff: inert until the persistence
// middleware (§11.5) fills it.
internal sealed class PersistenceMiddleware : Middleware;
