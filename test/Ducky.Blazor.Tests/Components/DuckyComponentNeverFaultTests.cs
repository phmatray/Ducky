using Bunit;
using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Tests.Components;

// SPEC §8.1, INV-10: DuckyComponent.DispatchAsync is in the never-fault set (the core property, NeverFaultTests, covers the
// rest of it). Non-normative: the Api_{Member}_NeverFaults manifest entry stays on Ducky.Tests at its stage.
public sealed class DuckyComponentNeverFaultTests : BunitContext
{
    public DuckyComponentNeverFaultTests() =>
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddSlice<ThrowingSlice>().Use<VetoBanned>());

    // Each call runs on the renderer through a rendered DuckyComponent<TState> and DuckyLayout: a throwing reducer, a
    // vetoing middleware, then the same three actions after the store's disposal began.
    [Fact]
    public async Task Api_DuckyComponentDispatchAsync_NeverFaults()
    {
        var component = Render<Sender>();
        var layout = Render<SenderLayout>(p => p.Add(c => c.Body, (RenderFragment)(_ => { })));
        var store = Services.GetRequiredService<IStore>();
        List<(Task<DispatchResult> Task, DispatchResult Expected)> calls = [];

        async Task SendAsync(object action, DispatchResult expected)
        {
            await component.InvokeAsync(() => calls.Add((component.Instance.Send(action), expected)));
            await layout.InvokeAsync(() => calls.Add((layout.Instance.Send(action), expected)));
        }

        await SendAsync(new Increment(), DispatchResult.Reduced);
        await SendAsync(new Boom(), DispatchResult.Failed);
        await SendAsync(new Banned(), DispatchResult.Vetoed);
        await Task.WhenAll(calls.Select(c => c.Task)).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        var disposal = store.DisposeAsync().AsTask();
        await SendAsync(new Increment(), DispatchResult.Disposed);
        await SendAsync(new Boom(), DispatchResult.Disposed);
        await SendAsync(new Banned(), DispatchResult.Disposed);
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await Task.WhenAll(calls.Select(c => c.Task)).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        calls.Count.ShouldBe(12);
        calls.ShouldAllBe(c => c.Task.IsCompletedSuccessfully && c.Task.Result == c.Expected);
    }
}

internal sealed record Boom;

internal sealed record Banned;

internal sealed record Untouched;

internal sealed class ThrowingSlice : Slice<Untouched>
{
    public ThrowingSlice() => On<Boom>(_ => throw new InvalidOperationException("reducer"));

    protected override Untouched Initial => new();
}

internal sealed class VetoBanned : Middleware
{
    public override bool MayDispatch(ActionContext context) => context.Action is not Banned;
}

internal sealed class Sender : DuckyComponent<Counter>
{
    public Task<DispatchResult> Send(object action) => DispatchAsync(action);

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Value);
}

internal sealed class SenderLayout : DuckyLayout
{
    public Task<DispatchResult> Send(object action) => DispatchAsync(action);
}
