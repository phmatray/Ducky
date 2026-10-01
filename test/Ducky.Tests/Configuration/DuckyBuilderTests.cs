using Ducky.Tests.BuilderFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §5.1 (DuckyBuilder, AddDucky), §8.1-8.2 (configuration errors); INV-31.
public sealed class DuckyBuilderTests
{
    private static readonly DuckyError _appError = new("APP001", "Marker is not registered.", "Register Marker.", "https://example.com/APP001");

    [Fact]
    public void Build_WithFiveMisconfigurations_ReportsAllFive()
    {
        var services = new ServiceCollection();

        // None of the five throws out of AddDucky, not even the two throwing slice constructors.
        services.AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Transient;
            d.AddSlice<CartSlice>()
                .AddSlice<BadKeySlice>()
                .AddSlice<CartCopySlice>()
                .AddSlice<NonConcreteHandlerSlice>()
                .AddSlice<DuplicateHandlerSlice>();
        });

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());
        exception.Errors.ShouldBe(
            [
                DuckyErrors.TransientLifetime(),
                DuckyErrors.InvalidKey(typeof(BadKeySlice), "Bad_Key"),
                DuckyErrors.DuplicateKey("cart", typeof(CartSlice), typeof(CartCopySlice)),
                DuckyErrors.NonConcreteHandlerType(typeof(NonConcreteHandlerSlice), typeof(AnyAction)),
                DuckyErrors.DuplicateHandler(typeof(DuplicateHandlerSlice), typeof(AddItem)),
            ],
            ignoreOrder: true);
        foreach (var error in exception.Errors)
        {
            exception.Message.ShouldContain(DuckyErrors.Format(error));
        }
    }

    [Fact]
    public void Build_SelfContainedAndDiDependentErrors_ReportedTogether()
    {
        var services = new ServiceCollection();
        IServiceProvider? seen = null;
        services.AddDucky(d => d
            .AddSlice<CartSlice>()
            .AddSlice<CartTwinSlice>()
            .AddSlice<BoxSlice<int>>()
            .AddValidation(sp =>
            {
                seen = sp;
                return sp.GetService<Marker>() is null ? [_appError] : [];
            })
            .AddValidation(_ => []));

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe(
            [
                DuckyErrors.DuplicateStateType(typeof(Cart), typeof(CartSlice), typeof(CartTwinSlice)),
                DuckyErrors.GenericSliceWithoutKey(typeof(BoxSlice<int>)),
                _appError,
            ],
            ignoreOrder: true);
        seen.ShouldNotBeNull();
    }

    [Fact]
    public void AddDucky_Twice_Throws()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<CartSlice>());
        var secondConfigureRan = false;

        var exception = Should.Throw<DuckyConfigurationException>(() => services.AddDucky(_ => secondConfigureRan = true));

        exception.Errors.ShouldBe([DuckyErrors.AddDuckyTwice()]);
        secondConfigureRan.ShouldBeFalse();
        services.Count(s => s.ServiceType == typeof(IStore)).ShouldBe(1);
    }

    // Non-normative: DUCKY300 means AddDucky ran twice, not that the app registered its own IStore (keyed or not).
    [Fact]
    public void AddDucky_AppRegisteredIStore_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IStore>("other", (_, _) => null!);

        Should.NotThrow(() => services.AddDucky(d => d.AddSlice<CartSlice>()));
    }

    // Non-normative: a valid configuration builds a store per resolution scope, each with its own slice instances in
    // registration order (AddSlice is idempotent), passing rules see the container, and the store logs through DI.
    [Fact]
    public async Task Build_ValidConfiguration_BuildsStoreFromRegisteredSlices()
    {
        var services = new ServiceCollection().AddSingleton<Marker>().AddLogging(logging => logging.AddFakeLogging());
        var builder = default(DuckyBuilder);
        var returned = services.AddDucky(d =>
        {
            builder = d;
            d.AddSlice<CartSlice>()
                .AddSlice<OrderSlice>()
                .AddSlice<CartSlice>()
                .AddSlice<KeyedBoxSlice<int>>()
                .AddValidation(sp => sp.GetService<Marker>() is null ? [_appError] : []);
        });
        returned.ShouldBeSameAs(services);
        builder!.Services.ShouldBeSameAs(services);

        await using var provider = services.BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var store = first.ServiceProvider.GetRequiredService<IStore>();
        var other = second.ServiceProvider.GetRequiredService<IStore>();

        store.Slices.Select(s => s.GetType()).ShouldBe([typeof(CartSlice), typeof(OrderSlice), typeof(KeyedBoxSlice<int>)]);
        other.ShouldNotBeSameAs(store);
        other.Slices[0].ShouldNotBeSameAs(store.Slices[0]);
        (await store.DispatchAsync(new AddItem())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Cart>().Items.ShouldBe(1);
        other.State.Get<Cart>().Items.ShouldBe(0);

        (await store.DispatchAsync(new Explode())).ShouldBe(DispatchResult.Failed);
        provider.GetRequiredService<FakeLogCollector>().GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1000);
    }

    // Non-normative: without logging in the container the store still builds and runs.
    [Fact]
    public async Task Build_WithoutLogging_UsesNullLogger()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<CartSlice>());

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        (await store.DispatchAsync(new Explode())).ShouldBe(DispatchResult.Failed);
    }

    // Non-normative: the §5.1 defaults. The lifetime table, with Warning 1004, is in StoreIdentityTests.
    [Fact]
    public void Builder_Defaults_MatchSpec()
    {
        var services = new ServiceCollection();
        var builder = default(DuckyBuilder);
        services.AddDucky(d => builder = d);

        builder!.MaxDispatchDepth.ShouldBe(64);
        builder.ThrowOnUnhandledAction.ShouldBeFalse();
        builder.InitBufferCapacity.ShouldBe(1024);
        builder.InitTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        builder.DisposeTimeout.ShouldBe(TimeSpan.FromSeconds(2));
        builder.IsBrowser.ShouldBeFalse();
    }

    // Non-normative: a nested AddDucky inside configure is the second call, so the outer one throws DUCKY300.
    [Fact]
    public void AddDucky_NestedInConfigure_Throws()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<DuckyConfigurationException>(() =>
            services.AddDucky(d => d.Services.AddDucky(inner => inner.AddSlice<CartSlice>())));

        exception.Errors.ShouldBe([DuckyErrors.AddDuckyTwice()]);
        services.Count(s => s.ServiceType == typeof(IStore)).ShouldBe(1);
    }

    // Non-normative (§8.1 "exception, once"): rules run once, at the first resolution, not once per scope.
    [Fact]
    public async Task Build_ScopedLifetime_ValidatesOnce()
    {
        var services = new ServiceCollection();
        var runs = 0;
        services.AddDucky(d => d.AddSlice<CartSlice>().AddValidation(_ =>
        {
            runs++;
            return [];
        }));

        await using var provider = services.BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var store = first.ServiceProvider.GetRequiredService<IStore>();

        second.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeSameAs(store);
        runs.ShouldBe(1);
    }

    // INV-31, §8.1: validation belongs to each container, so a second provider built from the same collection, changed
    // in between, runs the rules again against its own services and never sees the first provider's result.
    [Fact]
    public void Build_TwoProvidersFromOneCollection_EachValidatesItsOwnServices()
    {
        var services = new ServiceCollection();
        var runs = 0;
        services.AddDucky(d => d.AddValidation(sp =>
        {
            runs++;
            return sp.GetService<Marker>() is null ? [_appError] : [];
        }));

        using var first = services.BuildServiceProvider();
        Should.Throw<DuckyConfigurationException>(() => first.GetRequiredService<IStore>());
        services.AddSingleton<Marker>();
        using var second = services.BuildServiceProvider();

        second.GetRequiredService<IStore>().ShouldNotBeNull();
        runs.ShouldBe(2);
    }

    // Non-normative: an invalid configuration throws the same exception at every resolution, validated once.
    [Fact]
    public void Build_InvalidConfiguration_EveryResolutionThrowsSameException()
    {
        var services = new ServiceCollection();
        var runs = 0;
        services.AddDucky(d => d.AddValidation(_ =>
        {
            runs++;
            return [_appError];
        }));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>()).ShouldBeSameAs(first);
        runs.ShouldBe(1);
    }

    // INV-31: an ordinary exception from a slice constructor never leaves AddDucky; it is the inner exception of the
    // aggregated report, unwrapped from the reflection wrapper, and the message names the slice next to the coded errors.
    [Fact]
    public void Build_SliceCtorThrows_ReportedWithOtherErrors()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<ThrowingCtorSlice>().AddSlice<BadKeySlice>());

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe([DuckyErrors.InvalidKey(typeof(BadKeySlice), "Bad_Key")]);
        exception.Message.ShouldBe(
            DuckyErrors.Format(exception.Errors[0]) + Environment.NewLine
            + $"Slice {typeof(ThrowingCtorSlice).FullName} threw System.InvalidOperationException: ctor See the inner exception.");
        exception.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("ctor");
    }

    // INV-31: a null Key is DUCKY303, and a throwing Key getter or rule doesn't hide the errors already found; the message
    // names each failing slice and rule with what it threw.
    [Fact]
    public void Build_ThrowingUserCode_ReportedWithOtherErrors()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d
            .AddSlice<NullKeySlice>()
            .AddSlice<ThrowingKeySlice>()
            .AddSlice<ThrowingCtorSlice>()
            .AddValidation(_ => [_appError])
            .AddValidation(_ => throw new NotSupportedException("rule")));

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBe([DuckyErrors.InvalidKey(typeof(NullKeySlice), null!), _appError], ignoreOrder: true);
        exception.InnerException.ShouldBeOfType<AggregateException>().InnerExceptions.Select(e => e.GetType())
            .ShouldBe([typeof(FormatException), typeof(InvalidOperationException), typeof(NotSupportedException)]);
        exception.Message.Split(Environment.NewLine).ShouldBe(
            [
                .. exception.Errors.Select(DuckyErrors.Format),
                $"Slice {typeof(ThrowingKeySlice).FullName} threw System.FormatException: key See the inner exception.",
                $"Slice {typeof(ThrowingCtorSlice).FullName} threw System.InvalidOperationException: ctor See the inner exception.",
                "AddValidation rule #2 threw System.NotSupportedException: rule See the inner exception.",
            ]);
    }

    // INV-31: when user code threw and nothing else is wrong, the report still has a message that names the failure.
    [Fact]
    public void Build_OnlySliceCtorThrows_MessageNamesInnerException()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<CartSlice>().AddSlice<ThrowingCtorSlice>());

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBeEmpty();
        exception.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("ctor");
        exception.Message.ShouldBe(
            $"Slice {typeof(ThrowingCtorSlice).FullName} threw System.InvalidOperationException: ctor See the inner exception.");
    }

    // INV-31: same for a rule that throws.
    [Fact]
    public void Build_OnlyRuleThrows_MessageNamesInnerException()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddValidation(_ => throw new NotSupportedException("rule")));

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.ShouldBeEmpty();
        exception.InnerException.ShouldBeOfType<NotSupportedException>();
        exception.Message.ShouldBe("AddValidation rule #1 threw System.NotSupportedException: rule See the inner exception.");
    }

    // Non-normative: a rule that resolves the store re-enters validation on the same thread; it fails loudly instead of hanging.
    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    public void Build_RuleResolvesStore_ReportedInsteadOfHanging(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDucky(d =>
        {
            d.Lifetime = lifetime;
            d.AddValidation(sp => sp.GetService<IStore>() is null ? [_appError] : []);
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var exception = Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>());

        exception.Errors.ShouldBeEmpty();
        exception.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("AddValidation");
        exception.Message.ShouldContain("AddValidation");
    }

    // Non-normative: AddDucky snapshots the builder, so a builder that leaks out of configure changes nothing later.
    [Fact]
    public void Builder_ChangedAfterAddDucky_DoesNotAffectRegistration()
    {
        var services = new ServiceCollection();
        var builder = default(DuckyBuilder);
        services.AddDucky(d => builder = d.AddSlice<CartSlice>());

        builder!.Lifetime = ServiceLifetime.Transient;
        builder.AddSlice<OrderSlice>().AddValidation(_ => [_appError]);

        services.Single(s => s.ServiceType == typeof(IStore)).Lifetime.ShouldBe(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IStore>().Slices.ShouldHaveSingleItem().ShouldBeOfType<CartSlice>();
    }

    // Non-normative: null arguments are programmer errors, thrown synchronously.
    [Fact]
    public void NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddDucky(_ => { })).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(null!)).ParamName.ShouldBe("configure");
        new ServiceCollection().AddDucky(d =>
            Should.Throw<ArgumentNullException>(() => d.AddValidation(null!)).ParamName.ShouldBe("rule"));
    }

    // Non-normative (§5.1, INV-06): MaxDispatchDepth reaches the store's depth guard. With 1, depths 0 and 1 are
    // reduced and the reducer's dispatch at depth 2 is dropped.
    [Fact]
    public async Task Build_MaxDispatchDepth_ReachesDepthGuard()
    {
        var services = new ServiceCollection();
        services.AddDucky(d =>
        {
            d.MaxDispatchDepth = 1;
            d.AddSlice<DispatcherFixtures.StepSlice>();
        });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var children = new List<Task<DispatchResult>>();
        store.Slices.ShouldHaveSingleItem().ShouldBeOfType<DispatcherFixtures.StepSlice>().OnStep = _ =>
        {
            // Safety cap: without the guard this test fails instead of hanging.
            if (children.Count < 10)
            {
                children.Add(store.DispatchAsync(new DispatcherFixtures.Step("loop")));
            }
        };

        (await store.DispatchAsync(new DispatcherFixtures.Step("loop"))).ShouldBe(DispatchResult.Reduced);
        (await Task.WhenAll(children)).ShouldBe([DispatchResult.Reduced, DispatchResult.Dropped]);
    }
}
