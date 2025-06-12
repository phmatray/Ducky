// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace Ducky.Reactive.Patterns;

/// <summary>
/// A reactive effect that orchestrates complex workflows with multiple steps.
/// </summary>
/// <typeparam name="TStartAction">The action type that starts the workflow.</typeparam>
public abstract class WorkflowEffect<TStartAction> : ReactiveEffectBase
    where TStartAction : class
{
    /// <summary>
    /// Handles the effect by starting workflows when start actions are received.
    /// </summary>
    protected sealed override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        return actions
            .OfActionType<TStartAction>()
            .SwitchSelect(startAction => ExecuteWorkflow(startAction, actions, rootState));
    }

    /// <summary>
    /// Executes the workflow for a given start action.
    /// </summary>
    /// <param name="startAction">The action that started the workflow.</param>
    /// <param name="actions">The stream of all actions.</param>
    /// <param name="rootState">The stream of root state.</param>
    /// <returns>An observable of actions produced by the workflow.</returns>
    protected abstract IObservable<object> ExecuteWorkflow(
        TStartAction startAction,
        IObservable<object> actions,
        IObservable<IRootState> rootState);

    /// <summary>
    /// Creates a workflow step that executes sequentially.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="step">The step implementation.</param>
    /// <returns>An observable that emits step start, result, and completion actions.</returns>
    protected IObservable<object> Step(string stepName, Func<IObservable<object>> step)
    {
        return Observable.Defer(() =>
        {
            IObservable<object> stepStarted = Observable.Return(CreateStepStartedAction(stepName));
            IObservable<object> stepExecution = step()
                .Catch<object, Exception>(ex => Observable.Return(CreateStepFailedAction(stepName, ex)));
            IObservable<object> stepCompleted = Observable.Return(CreateStepCompletedAction(stepName));

            return Observable.Concat(stepStarted, stepExecution, stepCompleted);
        });
    }

    /// <summary>
    /// Creates a conditional workflow branch.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="trueBranch">The branch to execute if condition is true.</param>
    /// <param name="falseBranch">The branch to execute if condition is false.</param>
    /// <returns>An observable of actions from the selected branch.</returns>
    protected IObservable<object> Branch(
        Func<bool> condition,
        Func<IObservable<object>> trueBranch,
        Func<IObservable<object>> falseBranch)
    {
        return Observable.Defer(() => condition() ? trueBranch() : falseBranch());
    }

    /// <summary>
    /// Creates a parallel workflow execution.
    /// </summary>
    /// <param name="branches">The branches to execute in parallel.</param>
    /// <returns>An observable of actions from all branches.</returns>
    protected IObservable<object> Parallel(params Func<IObservable<object>>[] branches)
    {
        return Observable.Merge(branches.Select(branch => branch()));
    }

    /// <summary>
    /// Creates an action indicating a workflow step has started.
    /// Override to customize the action type.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <returns>The step started action.</returns>
    protected virtual object CreateStepStartedAction(string stepName)
    {
        return new WorkflowStepStarted(stepName);
    }

    /// <summary>
    /// Creates an action indicating a workflow step has completed.
    /// Override to customize the action type.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <returns>The step completed action.</returns>
    protected virtual object CreateStepCompletedAction(string stepName)
    {
        return new WorkflowStepCompleted(stepName);
    }

    /// <summary>
    /// Creates an action indicating a workflow step has failed.
    /// Override to customize the action type.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="error">The error that occurred.</param>
    /// <returns>The step failed action.</returns>
    protected virtual object CreateStepFailedAction(string stepName, Exception error)
    {
        return new WorkflowStepFailed(stepName, error.Message);
    }
}

/// <summary>
/// Default workflow step started action.
/// </summary>
/// <param name="StepName">The name of the step that started.</param>
public record WorkflowStepStarted(string StepName);

/// <summary>
/// Default workflow step completed action.
/// </summary>
/// <param name="StepName">The name of the step that completed.</param>
public record WorkflowStepCompleted(string StepName);

/// <summary>
/// Default workflow step failed action.
/// </summary>
/// <param name="StepName">The name of the step that failed.</param>
/// <param name="Error">The error message.</param>
public record WorkflowStepFailed(string StepName, string Error);
