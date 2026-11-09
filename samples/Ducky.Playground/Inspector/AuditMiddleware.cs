namespace Ducky.Playground.Inspector;

// The inspector's feed, on the public Middleware API. AfterReduce runs for every action the store processed, whatever
// its origin (a component, an effect, hydration, another tab), and also for an action whose reducer threw: that one
// committed nothing, and the ReducerFailed that follows names it.
public sealed class AuditMiddleware(Tape tape) : Middleware
{
    public override void AfterReduce(ActionContext context)
    {
        tape.Reduced(context);
        if (context.Action is ReducerFailed failed)
        {
            tape.Failed(failed.ActionType, context.CorrelationId, failed.Exception.Message);
        }
    }
}
