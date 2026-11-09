// The inspector is fed by a middleware. AfterReduce runs for every action the store
// processed, whatever its origin: a component, an effect, hydration or another tab.
namespace Ducky.Playground.Inspector;

public sealed class AuditMiddleware(Tape tape) : Middleware
{
    // It also runs for an action whose reducer threw. That action committed nothing,
    // and the ReducerFailed that follows names it, so its row turns Failed.
    public override void AfterReduce(ActionContext context)
    {
        tape.Reduced(context);
        if (context.Action is ReducerFailed failed)
        {
            tape.Failed(
                failed.ActionType,
                context.CorrelationId,
                failed.Exception.Message);
        }
    }
}
