using System.Globalization;
using System.Reflection;

namespace Ducky.Blazor.Tests.Core;

// SPEC §8.2, §8.3 (INV-31): Ducky.Blazor's runtime codes.
public sealed class ErrorModelTests
{
    private const string DocsRoot = "https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/";
    private const string ViewName = "Ducky.Blazor.Tests.Core.ErrorModelTests.View";

    // Every Ducky.Blazor runtime code: the catalogue factory, the error, its code, the concrete types its message names,
    // and what its fix names. Built per call, never cached in a static: Stryker switches the active mutant in-process.
    private static (string Factory, DuckyError Error, string Code, string[] InMessage, string[] InFix)[] Rows() =>
    [
        (nameof(BlazorErrors.SelectAfterFirstRender), BlazorErrors.SelectAfterFirstRender(typeof(View)), "DUCKY352",
            [$"Select was called on {ViewName} after its first render"], [$"OnInitialized of {ViewName}", "Selection<T>"]),
        (nameof(BlazorErrors.SelectAfterFirstRender), BlazorErrors.SelectAfterFirstRender(typeof(Generic<View[]>)), "DUCKY352",
            [$"on Ducky.Blazor.Tests.Core.ErrorModelTests.Generic<{ViewName}[]> after"], [$"of Ducky.Blazor.Tests.Core.ErrorModelTests.Generic<{ViewName}[]> and"]),
        (nameof(BlazorErrors.SelectAfterFirstRender), BlazorErrors.SelectAfterFirstRender(typeof(Generic<List<View>[,]>)), "DUCKY352",
            [$"on Ducky.Blazor.Tests.Core.ErrorModelTests.Generic<System.Collections.Generic.List<{ViewName}>[,]> after"], []),
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
            rows.Select(row => row.Code).Distinct().ShouldBe(["DUCKY352"]);

            // Every catalogue factory has a row, so a new factory can't skip the template.
            typeof(BlazorErrors).GetMethods(BindingFlags.Public | BindingFlags.Static)
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
                BlazorErrors.Format(error).ShouldBe($"{code}: {error.Message} {error.Fix} {error.HelpLink}");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    private static void ShouldBeSentence(string text, string code)
    {
        text.ShouldNotBeNullOrWhiteSpace(code);
        char.IsUpper(text[0]).ShouldBeTrue($"{code}: '{text}' starts with a capital");
        text.ShouldEndWith(".", Case.Sensitive, code);
        text.ShouldNotContain('\n', code);
    }

    private sealed class View;

    private sealed class Generic<T>;
}
