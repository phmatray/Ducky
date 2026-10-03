using Ducky.Tests.BuilderFixtures;
using Ducky.Tests.CtorCheckFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §5.1 (constructor dependencies are checked at first resolution), §8.2 (DUCKY309); INV-31.
public sealed class CtorCheckTests
{
    private const string ByAddEffect = "AddEffect<T>";

    [Fact]
    public void Build_EffectCtorDependencyMissing_ReportedWithOtherErrors()
    {
        var services = new ServiceCollection().AddSingleton<Marker>();
        services.AddDucky(d => d
            .AddSlice<CartSlice>()
            .AddSlice<CartTwinSlice>()
            .AddEffect<MissingServiceEffect>()
            .AddEffect<ResolvableEffect>()

            // Replaced by an instance whatever the order: the store never constructs it, so it is not checked.
            .AddEffect<ReplacedEffect>()
            .AddEffect(new ReplacedEffect(new Unregistered()))
            .RequireResolvableConstructor(typeof(QueuedType), "AddReactiveEffect<T>")
            .RequireResolvableConstructor(typeof(QueuedType), "AddReactiveEffect<T>"));

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe(
            [
                DuckyErrors.DuplicateStateType(typeof(Cart), typeof(CartSlice), typeof(CartTwinSlice)),
                DuckyErrors.UnresolvableConstructor(typeof(MissingServiceEffect), ByAddEffect, typeof(Unregistered), null),
                DuckyErrors.UnresolvableConstructor(typeof(QueuedType), "AddReactiveEffect<T>", typeof(Unregistered), null),
            ],
            ignoreOrder: true);
    }

    // Non-normative: a middleware's constructor dependencies are checked like an effect's (DUCKY309).
    [Fact]
    public void Build_MiddlewareCtorDependencyMissing_Reported()
    {
        var services = new ServiceCollection().AddSingleton<Marker>();
        services.AddDucky(d => d
            .AddSlice<CartSlice>()
            .Use<MissingServiceMiddleware>()
            .Use<ResolvableMiddleware>()
            .Use<MissingServiceMiddleware>());

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe([DuckyErrors.UnresolvableConstructor(typeof(MissingServiceMiddleware), "Use<T>", typeof(Unregistered), null)]);
    }

    // Non-normative: a middleware constructor that uses the store re-enters materialization, so that store call throws
    // InvalidOperationException; the constructor throwing it becomes DUCKY353, as for an effect (§6.6, §7 rule 4).
    [Fact]
    public async Task Materialization_MiddlewareCtorUsesStore_Throws()
    {
        List<Exception> thrown = [];
        var services = new ServiceCollection().AddSingleton(thrown);
        services.AddDucky(d => d.AddSlice<CartSlice>().Use<StoreUsingMiddleware>());
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        var exception = Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Ping()));

        var reentrant = thrown.ShouldHaveSingleItem().ShouldBeOfType<InvalidOperationException>();
        exception.InnerException.ShouldBeSameAs(reentrant);
        exception.Errors.ShouldBe([DuckyErrors.ConstructorThrew(typeof(StoreUsingMiddleware), reentrant)]);
        exception.Errors[0].Fix.ShouldBe(
            "Fix the constructor of Ducky.Tests.CtorCheckFixtures.StoreUsingMiddleware so that it does not throw; the inner exception has the details.");
        Should.Throw<DuckyConfigurationException>(() => _ = store.State).ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task Build_EffectCtorKeyedDependency_NotReported()
    {
        // Registered only under a key, like the .NET 9+ keyed HttpClient: IsService is false for Marker.
        var services = new ServiceCollection().AddKeyedSingleton<Marker>("github");
        services.AddDucky(d => d.AddSlice<CartSlice>().AddEffect<GithubEffect>());

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        // ActivatorUtilities builds it at the first use.
        store.State.Get<Cart>().Items.ShouldBe(0);
    }

    [Fact]
    public void Build_EffectCtorKeyedDependencyMissingKey_Reported()
    {
        var services = new ServiceCollection().AddSingleton<Marker>().AddKeyedSingleton<Marker>("gitlab");
        services.AddDucky(d => d.AddSlice<CartSlice>().AddEffect<GithubEffect>());

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe([DuckyErrors.UnresolvableConstructor(typeof(GithubEffect), ByAddEffect, typeof(Marker), "github")]);

        // The keyed wording, spelled out: comparing with the factory alone can't catch a wrong branch in it.
        var error = exception.Errors.ShouldHaveSingleItem();
        error.Message.ShouldEndWith("can't be constructed: no constructor can resolve its Ducky.Tests.BuilderFixtures.Marker parameter with key 'github'.");
        error.Fix.ShouldBe("Register Ducky.Tests.BuilderFixtures.Marker as a keyed service with key 'github', or give that parameter a default value.");
    }

    // Non-normative: a [ServiceKey] parameter never resolves, a defaulted one is never needed, and the candidates are
    // the ones ActivatorUtilities would pick: the [ActivatorUtilitiesConstructor] one alone, else every public one.
    [Fact]
    public void CtorCheck_Candidates_FollowActivatorUtilities()
    {
        var services = new ServiceCollection().AddSingleton<Marker>();
        services.AddDucky(d => d
            .AddSlice<CartSlice>()
            .RequireResolvableConstructor(typeof(ServiceKeyType), "test")
            .RequireResolvableConstructor(typeof(TwoMissing), "test")
            .RequireResolvableConstructor(typeof(SecondCtorResolvable), "test")
            .RequireResolvableConstructor(typeof(MarkedCtorUnresolvable), "test")
            .RequireResolvableConstructor(typeof(NoCtorResolvable), "test")
            .RequireResolvableConstructor(typeof(NoPublicCtor), "test"));

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe(
            [
                DuckyErrors.ServiceKeyParameter(typeof(ServiceKeyType), "test", "key"),
                DuckyErrors.UnresolvableConstructor(typeof(TwoMissing), "test", typeof(Unregistered), null),
                DuckyErrors.UnresolvableConstructor(typeof(MarkedCtorUnresolvable), "test", typeof(Unregistered), null),
                DuckyErrors.UnresolvableConstructor(typeof(NoCtorResolvable), "test", typeof(Unregistered), null),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task CtorCheck_NoIsServiceProvider_SkippedWithDebugLog()
    {
        var logs = await CreateHidingAsync(d => d.AddEffect<MissingServiceEffect>(), typeof(IServiceProviderIsService));

        var record = logs.ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1030);
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"Constructor check of {typeof(MissingServiceEffect)} skipped: the container offers no {typeof(IServiceProviderIsService)}");
    }

    // Non-normative: without IServiceProviderIsKeyedService only the keyed parameter is skipped.
    [Fact]
    public async Task CtorCheck_NoIsKeyedServiceProvider_KeyedParameterSkippedWithDebugLog()
    {
        var logs = await CreateHidingAsync(d => d.AddEffect<GithubEffect>(), typeof(IServiceProviderIsKeyedService));

        var record = logs.ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1030);
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"Constructor check of {typeof(GithubEffect)} skipped: the container offers no {typeof(IServiceProviderIsKeyedService)}");
    }

    // Non-normative: the other parameters of that constructor are still checked.
    [Fact]
    public async Task CtorCheck_NoIsKeyedServiceProvider_OtherParametersStillChecked()
    {
        var services = new ServiceCollection().AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug));
        services.AddDucky(d => d.AddSlice<CartSlice>().AddEffect<KeyedPlusMissingEffect>());
        await using var provider = services.BuildServiceProvider();
        var factory = services.Single(descriptor => descriptor.ServiceType == typeof(IStore)).ImplementationFactory!;

        var exception = Should.Throw<DuckyConfigurationException>(() => factory(new HidingProvider(provider, typeof(IServiceProviderIsKeyedService))));

        exception.Errors.ShouldBe([DuckyErrors.UnresolvableConstructor(typeof(KeyedPlusMissingEffect), ByAddEffect, typeof(Unregistered), null)]);
        var record = provider.GetRequiredService<FakeLogCollector>().GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1030);
        record.Message.ShouldBe($"Constructor check of {typeof(KeyedPlusMissingEffect)} skipped: the container offers no {typeof(IServiceProviderIsKeyedService)}");
    }

    [Fact]
    public void RequireResolvableConstructor_NullArguments_Throw() =>
        new ServiceCollection().AddDucky(d =>
        {
            Should.Throw<ArgumentNullException>(() => d.RequireResolvableConstructor(null!, "x")).ParamName.ShouldBe("type");
            Should.Throw<ArgumentNullException>(() => d.RequireResolvableConstructor(typeof(QueuedType), null!)).ParamName.ShouldBe("requiredBy");
        });

    // Runs the IStore factory against a container that hides `hidden`, as a third-party container may. Nothing is
    // registered for Marker, so only a skipped check lets the store build. The store is never used: nothing is constructed.
    private static async Task<IReadOnlyList<FakeLogRecord>> CreateHidingAsync(Action<DuckyBuilder> configure, Type hidden)
    {
        var services = new ServiceCollection().AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug));
        services.AddDucky(d => configure(d.AddSlice<CartSlice>()));
        await using var provider = services.BuildServiceProvider();
        var factory = services.Single(descriptor => descriptor.ServiceType == typeof(IStore)).ImplementationFactory!;
        await using var store = (DuckyStore)factory(new HidingProvider(provider, hidden));
        return provider.GetRequiredService<FakeLogCollector>().GetSnapshot();
    }

    private sealed class HidingProvider(IServiceProvider inner, Type hidden) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == hidden ? null : inner.GetService(serviceType);
    }
}
