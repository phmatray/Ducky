using System.Text.Json.Nodes;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Generated.Tests;

// SPEC §16.1 (GEN-02 [Persist]), §11.1 (PersistAttributeDefaults<T>, AddBlazor composition), ADR-0012: the calls the
// generator emits from this project's [Persist]/[Prerender] fixtures, composed with builder calls in either order.
public sealed class GeneratedPersistTests
{
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };

    private static readonly Func<JsonNode, JsonNode> _same = static node => node;

    [Fact]
    public async Task PersistAttribute_NoArguments_SlicePersisted()
    {
        // A bare [Persist] registers the slice as persisted exactly like Persist<T>(): Ducky.Blazor added, every default
        // kept, validated with the other slices; the [Prerender] slice is prerendered.
        await using var provider = Build(static d => Migrated(d.AddDuckyGenerated_Ducky_Blazor_Generated_Tests()));
        await using (var scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IStore>().Slices.Select(static slice => slice.GetType())
                .ShouldBe([typeof(NotesSlice), typeof(CartSlice), typeof(TodosSlice), typeof(PersistenceSlice)], ignoreOrder: true);
        }

        var registration = provider.GetRequiredService<BlazorRegistration>();
        registration.Persist.Keys.ShouldBe([typeof(NotesSlice), typeof(CartSlice)], ignoreOrder: true);
        var notes = registration.Persist[typeof(NotesSlice)].Options;
        (notes.Storage, notes.Version, notes.MaxAge, notes.Debounce, notes.SyncAcrossTabs).ShouldBe((PersistStorage.Local, 1, null, null, false));
        notes.Migrations.ShouldBeEmpty();
        registration.Prerender.Keys.ShouldBe([typeof(TodosSlice)]);

        // It requires the state's JSON type info as Persist<T> does (DUCKY306).
#pragma warning disable DUCKY006 // justification: the context deliberately lacks Notes, to observe the run-time DUCKY306
        using var missing = Build(static d => Migrated(d.AddDuckyGenerated_Ducky_Blazor_Generated_Tests()).UseJson(WithoutNotesJson.Default));
#pragma warning restore DUCKY006
        using var failing = missing.CreateScope();
        var error = Should.Throw<DuckyConfigurationException>(() => failing.ServiceProvider.GetRequiredService<IStore>()).Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("DUCKY306");
        error.Message.ShouldContain(typeof(Notes).FullName!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersistAttributePlusBuilderMigrate_Composes(bool generatedFirst)
    {
        // The attribute's Version 2 combines with the builder's Migrate(1, ...) whatever the call order, and on the
        // overlapping SyncAcrossTabs the builder wins (ADR-0012).
        Action<DuckyBuilder> builder = static d => d.Persist<CartSlice>(static o =>
        {
            o.Migrate(1, _same);
            o.SyncAcrossTabs = false;
        });
        await using var provider = Build(d =>
        {
            if (generatedFirst)
            {
                builder(d.AddDuckyGenerated_Ducky_Blazor_Generated_Tests());
            }
            else
            {
                builder(d);
                d.AddDuckyGenerated_Ducky_Blazor_Generated_Tests();
            }
        });
        await using (var scope = provider.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
        }

        var cart = provider.GetRequiredService<BlazorRegistration>().Persist[typeof(CartSlice)].Options;
        (cart.Storage, cart.Version, cart.SyncAcrossTabs).ShouldBe((PersistStorage.Local, 2, false));
        cart.Migrations.Keys.ShouldBe([1]);
        cart.Migrations[1].ShouldBeSameAs(_same);
    }

    [Fact]
    public async Task AddBlazor_AfterGeneratedPersist_ConfigureApplied()
    {
        // The usual chain: the generated PersistAttributeDefaults<T> registers Ducky.Blazor with AddBlazor() before the
        // user's AddBlazor(configure), whose options still apply, so its Scope keeps users' browser keys apart.
        Func<IServiceProvider, CancellationToken, ValueTask<string?>> scope = static (_, _) => ValueTask.FromResult<string?>("tenant");
        await using var provider = Build(d => Migrated(d.AddDuckyGenerated_Ducky_Blazor_Generated_Tests().AddBlazor(o =>
        {
            o.KeyPrefix = "app";
            o.Scope = scope;
        })));
        await using (var resolved = provider.CreateAsyncScope())
        {
            resolved.ServiceProvider.GetRequiredService<IStore>().Slices.OfType<PersistenceSlice>().ShouldHaveSingleItem();
        }

        var registration = provider.GetRequiredService<BlazorRegistration>();
        registration.Persist.Keys.ShouldContain(typeof(NotesSlice));
        var options = provider.GetRequiredService<BlazorOptions>();
        options.ShouldBeSameAs(registration.Options);
        options.KeyPrefix.ShouldBe("app");
        options.Scope.ShouldBeSameAs(scope);
    }

    private static DuckyBuilder Migrated(DuckyBuilder builder) => builder.Persist<CartSlice>(static o => o.Migrate(1, _same));

    private static ServiceProvider Build(Action<DuckyBuilder> configure) =>
        new ServiceCollection().AddDucky(d => configure(d.UseJson(GeneratedJson.Default))).BuildServiceProvider(_validating);
}
