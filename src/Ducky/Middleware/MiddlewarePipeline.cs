using System.Collections.ObjectModel;

namespace Ducky;

// SPEC §6.4 steps 4, 5 and 9: the middleware hooks, in registration order, each call isolated. The store creates the
// middleware lazily, on its first use (§6.6); phase 5a of dispose (Dispose.cs) disposes it.
internal sealed partial class Dispatcher
{
    // Step 4, only for Local and Effect: the first false completes Vetoed, a throw is a failure that completes Failed.
    // Either way the action was never admitted, so Process ends (§6.4).
    private bool Admitted(Pending p, ActionContext context, Middleware[] middleware)
    {
        if (p.Origin is not (Origin.Local or Origin.Effect))
        {
            return true;
        }

        foreach (var m in middleware)
        {
            bool allowed;
            try
            {
                allowed = m.MayDispatch(context);
            }
#pragma warning disable CA1031 // justification: a hook is user code; its throw fails the action, never the drain (§6.4)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                p.Complete(DispatchResult.Failed);
                HookFailed(m, nameof(Middleware.MayDispatch), context.Action, ex);
                return false;
            }

            if (!allowed)
            {
                p.Complete(DispatchResult.Vetoed);
                Log.Vetoed(logger, context.Action.GetType(), m.GetType());
                return false;
            }
        }

        return true;
    }

    // Step 5: the first throw is the action's one failure (INV-12), so the later middleware are not called.
    private bool BeforeReduce(ActionContext context, Middleware[] middleware)
    {
        foreach (var m in middleware)
        {
            try
            {
                m.BeforeReduce(context);
            }
#pragma warning disable CA1031 // justification: a hook is user code; its throw fails the action, never the drain (§6.4)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                HookFailed(m, nameof(Middleware.BeforeReduce), context.Action, ex);
                return false;
            }
        }

        return true;
    }

    // Step 9: a throw is logged and the other middleware still run.
    private void AfterReduce(ActionContext context, Middleware[] middleware)
    {
        foreach (var m in middleware)
        {
            try
            {
                m.AfterReduce(context);
            }
#pragma warning disable CA1031 // justification: a hook is user code; its throw is logged, never the drain's (§6.4)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.MiddlewareHookThrew(logger, ex, m.GetType(), nameof(Middleware.AfterReduce), context.Action.GetType());
            }
        }
    }

    // Steps 4-5 route the failure with the action's own scope, installed by step 2; a middleware failure has no slice key.
    private void HookFailed(Middleware m, string hook, object action, Exception ex)
    {
        Log.MiddlewareHookThrew(logger, ex, m.GetType(), hook, action.GetType());
        RouteScopedFailure(action, null, ex);
    }

    // The keys whose slot the commit changed: only scratch entries can differ from the previous snapshot. Read-only, since
    // every AfterReduce shares one context.
    private ReadOnlyCollection<string> ChangedKeys(StateSnapshot previous, StateSnapshot state)
    {
        List<string> keys = [];
        foreach (var (ordinal, _) in _scratch)
        {
            if (!ReferenceEquals(previous[ordinal], state[ordinal]))
            {
                keys.Add(registry.Keys[ordinal]);
            }
        }

        return keys.AsReadOnly();
    }
}
