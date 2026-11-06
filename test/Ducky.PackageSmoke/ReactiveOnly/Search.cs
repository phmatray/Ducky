using Ducky.Generated;
using Ducky.Reactive;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.PackageSmoke.ReactiveOnly;

/// <summary>A query the reactive effect answers.</summary>
/// <param name="Text">The query text.</param>
public sealed record Query(string Text);

/// <summary>The answer to a <see cref="Query"/>.</summary>
/// <param name="Text">The query text.</param>
public sealed record Answered(string Text);

/// <summary>A reactive effect, registered by the generated <c>AddReactiveEffect&lt;T&gt;</c> call.</summary>
public sealed class Search : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<Query>().Select(static query => (object)new Answered(query.Text));
}

/// <summary>Registers the store through the generated method.</summary>
public static class Startup
{
    /// <summary>Adds the store with every generated registration.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection Configure(IServiceCollection services) =>
        services.AddDucky(d => d.AddDuckyGenerated_Ducky_PackageSmoke_ReactiveOnly());
}
