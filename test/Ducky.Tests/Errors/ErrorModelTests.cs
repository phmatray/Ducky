using System.Globalization;
using System.Reflection;
using Ducky.Tests.Errors.Fixtures;

namespace Ducky.Tests;

// SPEC §5.7, §8.2, §8.3 (INV-31).
public sealed class ErrorModelTests
{
    private const string DocsRoot = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/";
    private const string Fixtures = "Ducky.Tests.Errors.Fixtures.";
    private const string Cart = $"{Fixtures}CartState";
    private const string CartSliceName = $"{Fixtures}CartSlice";
    private const string OtherCartSliceName = $"{Fixtures}OtherCartSlice";

    // Every Ducky runtime code (DUCKY352 and DUCKY310-317 are Ducky.Blazor's): the catalogue factory, the error, its code,
    // the concrete types and keys its message names, and what its fix names. Built per call, never cached in a static:
    // Stryker switches the active mutant inside one test process.
    private static (string Factory, DuckyError Error, string Code, string[] InMessage, string[] InFix)[] Rows() =>
    [
        (nameof(DuckyErrors.AddDuckyTwice), DuckyErrors.AddDuckyTwice(), "DUCKY300", ["AddDucky", "IServiceCollection"],
            ["AddDucky", "configure"]),
        (nameof(DuckyErrors.TransientLifetime), DuckyErrors.TransientLifetime(), "DUCKY301",
            ["DuckyBuilder.Lifetime", "ServiceLifetime.Transient"], ["ServiceLifetime.Singleton", "ServiceLifetime.Scoped"]),
        (nameof(DuckyErrors.GenericSliceWithoutKey), DuckyErrors.GenericSliceWithoutKey(typeof(ListSlice<CartState>)),
            "DUCKY302", [$"{Fixtures}ListSlice<{Cart}>"], [$"{Fixtures}ListSlice<{Cart}>", "Key"]),
        (nameof(DuckyErrors.GenericSliceWithoutKey), DuckyErrors.GenericSliceWithoutKey(typeof(ListSlice<>)), "DUCKY302",
            [$"Slice {Fixtures}ListSlice<T> is"], [$"{Fixtures}ListSlice<T> so"]),
        (nameof(DuckyErrors.InvalidKey), DuckyErrors.InvalidKey(typeof(CartSlice), "Cart_Items"), "DUCKY303",
            [CartSliceName, "'Cart_Items'"], ["^[a-z0-9]+(-[a-z0-9]+)*$", "@ducky/"]),
        (nameof(DuckyErrors.DuplicateKey), DuckyErrors.DuplicateKey("cart", typeof(CartSlice), typeof(OtherCartSlice)),
            "DUCKY304", [CartSliceName, OtherCartSliceName, "'cart'"], [OtherCartSliceName, "Key"]),
        (nameof(DuckyErrors.DuplicateStateType),
            DuckyErrors.DuplicateStateType(typeof(CartState), typeof(CartSlice), typeof(OtherCartSlice)), "DUCKY305",
            [CartSliceName, OtherCartSliceName, Cart], [OtherCartSliceName, $"State.Get<{Cart}>()"]),
        (nameof(DuckyErrors.MissingJsonTypeInfo), DuckyErrors.MissingJsonTypeInfo(typeof(CartState), "Persist<CartSlice>"),
            "DUCKY306", ["Persist<CartSlice>", Cart, "UseJson"], [$"[JsonSerializable(typeof({Cart}))]", "JsonSerializerContext"]),
        (nameof(DuckyErrors.MissingJsonTypeInfo),
            DuckyErrors.MissingJsonTypeInfo(typeof(List<CartState>[]), "Persist<CartSlice>"), "DUCKY306",
            [$"needs System.Collections.Generic.List<{Cart}>[],"],
            [$"[JsonSerializable(typeof(System.Collections.Generic.List<{Cart}>[]))]"]),
        (nameof(DuckyErrors.NullTypeInfoResolver), DuckyErrors.NullTypeInfoResolver(), "DUCKY306",
            ["UseJson", "JsonSerializerOptions", "TypeInfoResolver"], ["TypeInfoResolver", "UseJson"]),
        (nameof(DuckyErrors.NonConcreteHandlerType), DuckyErrors.NonConcreteHandlerType(typeof(CartSlice), typeof(IComparable)),
            "DUCKY307", [CartSliceName, "On<System.IComparable>()"], ["On<T>()", "System.IComparable"]),
        (nameof(DuckyErrors.DuplicateHandler), DuckyErrors.DuplicateHandler(typeof(CartSlice), typeof(Outer.Added)), "DUCKY308",
            [CartSliceName, $"On<{Fixtures}Outer.Added>()"], [CartSliceName, $"On<{Fixtures}Outer.Added>()"]),
        (nameof(DuckyErrors.DuplicateHandler), DuckyErrors.DuplicateHandler(typeof(Outer.Box<CartState>), typeof(CartState[,])),
            "DUCKY308", [$"{Fixtures}Outer.Box<{Cart}> calls On<{Cart}[,]>()"], [$"On<{Cart}[,]>() call in {Fixtures}Outer.Box<{Cart}>"]),
        (nameof(DuckyErrors.UnresolvableConstructor),
            DuckyErrors.UnresolvableConstructor(typeof(CartSlice), "AddEffect", typeof(Dictionary<string, int>), null),
            "DUCKY309", [CartSliceName, "AddEffect", "System.Collections.Generic.Dictionary<System.String, System.Int32>"],
            ["Register System.Collections.Generic.Dictionary<System.String, System.Int32> in"]),
        (nameof(DuckyErrors.UnresolvableConstructor), DuckyErrors.UnresolvableConstructor(typeof(CartSlice), "Use<T>",
                typeof(Func<int, int, int, int, int, int, int, int, int, int, int>), null), "DUCKY309",
            [$"its System.Func<{string.Join(", ", Enumerable.Repeat("System.Int32", 11))}> parameter"],
            [$"Register System.Func<{string.Join(", ", Enumerable.Repeat("System.Int32", 11))}> in"]),
        (nameof(DuckyErrors.UnresolvableConstructor),
            DuckyErrors.UnresolvableConstructor(typeof(CartSlice), "AddReactiveEffect", typeof(HttpClient), "github"),
            "DUCKY309", [CartSliceName, "AddReactiveEffect", "System.Net.Http.HttpClient", "key 'github'"],
            ["Register System.Net.Http.HttpClient as a keyed service with key 'github'"]),
        (nameof(DuckyErrors.UnresolvableConstructor),
            DuckyErrors.UnresolvableConstructor(typeof(CartSlice), "AddEffect", typeof(HttpClient), 1.5), "DUCKY309",
            ["with key '1.5'"], ["with key '1.5'"]),
        (nameof(DuckyErrors.ServiceKeyParameter), DuckyErrors.ServiceKeyParameter(typeof(CartSlice), "AddEffect", "tenant"),
            "DUCKY309", [CartSliceName, "AddEffect", "tenant", "[ServiceKey]"],
            ["Remove [ServiceKey] from tenant", "default value", "keyed effects or middleware"]),
        (nameof(DuckyErrors.UnregisteredState), DuckyErrors.UnregisteredState(typeof(CartState)), "DUCKY350", [Cart],
            ["AddDuckyGenerated_Ducky_Tests()", "Ducky.Tests", "AddSlice<TSlice>()"]),
        (nameof(DuckyErrors.SliceStoreNotAttached), DuckyErrors.SliceStoreNotAttached(typeof(CartSlice)), "DUCKY351",
            [CartSliceName, "new"], [CartSliceName, $"AddSlice<{CartSliceName}>()"]),
        (nameof(DuckyErrors.ConstructorThrew), DuckyErrors.ConstructorThrew(typeof(CartSlice), new FormatException("bad")),
            "DUCKY353", [CartSliceName, "System.FormatException"], [CartSliceName, "inner exception"]),
        (nameof(DuckyErrors.ConstructorThrew), DuckyErrors.ConstructorThrew(typeof(CartSlice), new DuckyConfigurationException(
                [
                    DuckyErrors.NonConcreteHandlerType(typeof(CartSlice), typeof(object)),
                    DuckyErrors.DuplicateHandler(typeof(CartSlice), typeof(CartState)),
                ])), "DUCKY353",
            [CartSliceName, "Ducky.DuckyConfigurationException (DUCKY307, DUCKY308)"], [CartSliceName, "inner exception"]),
    ];

    [Fact]
    public void EveryErrorCode_FollowsMessageTemplate()
    {
        // A culture with a decimal comma: error text never depends on the host culture.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var rows = Rows();
            rows.Select(row => row.Code).Distinct().ShouldBe(
            [
                "DUCKY300", "DUCKY301", "DUCKY302", "DUCKY303", "DUCKY304", "DUCKY305", "DUCKY306", "DUCKY307", "DUCKY308",
                "DUCKY309", "DUCKY350", "DUCKY351", "DUCKY353",
            ]);

            // Every catalogue factory has a row, so a new factory can't skip the template.
            typeof(DuckyErrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(DuckyError))
                .Select(method => method.Name)
                .ShouldBe(rows.Select(row => row.Factory).Distinct(), ignoreOrder: true);

            foreach (var (_, error, code, inMessage, inFix) in rows)
            {
                // 1. What happened and 2. the concrete types and keys, 3. the exact fix, 4. the link.
                error.Code.ShouldBe(code);
                ShouldBeSentence(error.Message, code);
                ShouldBeSentence(error.Fix, code);
                foreach (var name in inMessage)
                {
                    error.Message.ShouldContain(name, Case.Sensitive, $"{code} message");
                }

                foreach (var name in inFix)
                {
                    error.Fix.ShouldContain(name, Case.Sensitive, $"{code} fix");
                }

                error.HelpLink.ShouldBe($"{DocsRoot}{code}.md");

                var text = $"{code}: {error.Message} {error.Fix} {error.HelpLink}";
                new DuckyConfigurationException([error]).Message.ShouldBe(text);
            }

            // Without a service key, nothing mentions one; a [ServiceKey] parameter is never fixed by a registration.
            var unkeyed = DuckyErrors.UnresolvableConstructor(typeof(CartSlice), "AddEffect", typeof(HttpClient), null);
            unkeyed.Message.ShouldNotContain("key");
            unkeyed.Fix.ShouldNotContain("key");
            DuckyErrors.ServiceKeyParameter(typeof(CartSlice), "AddEffect", "tenant").Fix.ShouldNotContain("Register");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void DuckyConfigurationException_ListsEveryErrorAndKeepsInner()
    {
        var first = DuckyErrors.AddDuckyTwice();
        var second = DuckyErrors.TransientLifetime();
        var inner = new FormatException("bad");
        var errors = new List<DuckyError> { first, second };

        var exception = new DuckyConfigurationException(errors, inner);
        errors.Clear();

        exception.ShouldBeAssignableTo<InvalidOperationException>();
        exception.Errors.ShouldBe([first, second]);
        exception.InnerException.ShouldBeSameAs(inner);
        exception.Message.ShouldBe(
            $"DUCKY300: {first.Message} {first.Fix} {first.HelpLink}{Environment.NewLine}"
            + $"DUCKY301: {second.Message} {second.Fix} {second.HelpLink}");
        new DuckyConfigurationException([first]).InnerException.ShouldBeNull();
    }

    [Fact]
    public void LibraryExceptions_CarryActionTypeAndDepth()
    {
        var loop = new DispatchLoopException("cart/Add", 65);
        loop.ActionType.ShouldBe("cart/Add");
        loop.Depth.ShouldBe(65);
        loop.Message.ShouldBe(
            "Action 'cart/Add' was dropped at causal depth 65, above MaxDispatchDepth: something dispatches it in a "
            + "synchronous loop (a reducer, middleware, subscriber or effect prefix that dispatches what it handles).");

        var unhandled = new UnhandledActionException("cart/Add");
        unhandled.ActionType.ShouldBe("cart/Add");
        unhandled.Message.ShouldBe(
            "Action 'cart/Add' matched no reducer or async effect and ThrowOnUnhandledAction is set.");
    }

    [Fact]
    public void LibraryActions_CarryTheirValues()
    {
        var exception = new FormatException("bad");

        var reducer = new ReducerFailed("cart/Add", null, exception);
        (reducer.ActionType, reducer.SliceKey, reducer.Exception).ShouldBe(("cart/Add", (string?)null, exception));
        var failed = new EffectFailed("MyApp.LoadCart", "cart/Add", exception);
        (failed.EffectType, failed.ActionType, failed.Exception).ShouldBe(("MyApp.LoadCart", "cart/Add", exception));
    }

    private static void ShouldBeSentence(string text, string code)
    {
        text.ShouldNotBeNullOrWhiteSpace(code);
        char.IsUpper(text[0]).ShouldBeTrue($"{code}: '{text}' starts with a capital");
        text.ShouldEndWith(".", Case.Sensitive, code);
        text.ShouldNotContain('\n', code);
    }
}
