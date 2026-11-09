// The demonstration stock list that the fake ShopApi serves to the catalogue effect.
using System.Collections.Immutable;

namespace Ducky.Playground.Shop.Catalogue;

public static class Stockroom
{
    public const string ClassicId = "classic";

    // The fake payment provider declines any order holding this duck, so checkout can
    // fail without any rigging.
    public const string CursedId = "cursed-duck";

    public static readonly ImmutableList<Duck> Ducks =
    [
        new(ClassicId, "The Classic", DuckColour.Yellow, 6.50m, 48,
            ["bestseller"], "The one on every desk. Listens without interrupting."),
        new("null-reference", "Null Reference", DuckColour.Black, 7.00m, 12,
            ["debugging"], "Black, like the stack trace you meant to read."),
        new("pair-programmer", "Pair Programmer", DuckColour.Yellow, 12.00m, 9,
            ["set of two"], "Two ducks, one keyboard, fewer bugs."),
        new("code-reviewer", "Code Reviewer", DuckColour.White, 8.50m, 15,
            ["strict"], "Approves nothing after four on a Friday."),
        new("senior-duck", "Senior Duck", DuckColour.White, 14.00m, 4,
            ["veteran"], "Has watched a few frameworks come and go."),
        new("merge-conflict", "Merge Conflict", DuckColour.Red, 7.50m, 6,
            ["git"], "Arrives in two halves that don't quite agree."),
        new("hotfix", "Hotfix", DuckColour.Red, 9.00m, 2,
            ["rush"], "Shipped at two in the morning. Floats anyway."),
        new("off-by-one", "Off-by-One", DuckColour.Blue, 5.50m, 21,
            ["classic bug"], "One more duck than you expected. Or one fewer."),
        new("async-await", "Async Await", DuckColour.Blue, 7.00m, 18,
            ["effects"], "Comes back when it's ready. Never blocks the bath."),
        new("immutable", "Immutable", DuckColour.Lilac, 8.00m, 25,
            ["state"], "Never changes. Every edit is a new duck."),
        new("the-reducer", "The Reducer", DuckColour.Lilac, 8.00m, 0,
            ["state", "pure"], "Same input, same duck, every time."),
        new("side-effect", "Side Effect", DuckColour.Green, 7.50m, 11,
            ["effects"], "Does something somewhere else. Keep it in an effect."),
        new("legacy-duck", "Legacy Duck", DuckColour.Green, 4.00m, 3,
            ["vintage"], "Nobody knows who wrote it. It still floats."),
        new(CursedId, "The Cursed Duck", DuckColour.Black, 13.13m, 7,
            ["cursed"], "Our payment provider refuses to touch it. Try it."),
        new("deep-diver", "Deep Diver", DuckColour.Blue, 9.50m, 8,
            ["debugging"], "Goes to the bottom of every issue."),
        new("linter", "Linter", DuckColour.White, 5.00m, 40,
            ["strict"], "Points out the trailing whitespace on your soap."),
        new("standup-duck", "Standup Duck", DuckColour.Yellow, 6.00m, 14,
            ["daily"], "Yesterday: floated. Today: floating. Blockers: none."),
        new("ten-x-duck", "Ten-X Duck", DuckColour.Red, 19.00m, 1,
            ["limited"], "Said to do the work of ten ducks. Unverified."),
        new("feature-flag", "Feature Flag", DuckColour.Mint, 6.50m, 16,
            ["config"], "Sometimes there, sometimes not, depending on config."),
        new("dependency-injection", "Dependency Injection", DuckColour.Lilac, 9.00m, 10,
            ["effects"], "Doesn't build its own pond. Asks for one."),
        new("hydration", "Hydration", DuckColour.Blue, 6.50m, 22,
            ["blazor"], "Comes back exactly as you left it, even after a reload."),
        new("prerender", "Prerender", DuckColour.White, 7.00m, 9,
            ["blazor"], "Shows up on the page before it can quack."),
        new("circuit-breaker", "Circuit Breaker", DuckColour.Black, 8.50m, 5,
            ["resilience"], "Stops quacking after three failures in a row."),
        new("garbage-collector", "Garbage Collector", DuckColour.Green, 5.50m, 13,
            ["runtime"], "Tidies the tub when you aren't looking."),
        new("tech-debt", "Tech Debt", DuckColour.Green, 3.00m, 60,
            ["clearance"], "Cheap now. Expensive later."),
        new("spec-duck", "Spec Duck", DuckColour.Yellow, 8.00m, 7,
            ["tested"], "Every quack is a named test."),
        new("infinite-loop", "Infinite Loop", DuckColour.Red, 6.00m, 0,
            ["recursive"], "A duck holding a duck holding a duck."),
        new("lambda", "Lambda", DuckColour.Mint, 7.00m, 19,
            ["functional"], "Small, anonymous, gets the job done."),
        new("breakpoint", "Breakpoint", DuckColour.Red, 6.50m, 8,
            ["debugging"], "Stops right where it gets interesting."),
        new("golden-path", "Golden Path", DuckColour.Yellow, 11.00m, 6,
            ["rare"], "Everything works the first time."),
        new("night-shift", "Night Shift", DuckColour.Black, 7.50m, 9,
            ["ops"], "For the deploys nobody watches."),
        new("snapshot", "Snapshot", DuckColour.White, 6.00m, 20,
            ["state"], "A point-in-time duck. Selections follow the newer ones."),
        new("middleware", "Middleware", DuckColour.Lilac, 7.50m, 12,
            ["pipeline"], "Stands between you and the pond. Vetoes the eleventh duck."),
        new("time-traveller", "Time Traveller", DuckColour.Blue, 10.00m, 5,
            ["devtools"], "Jumps back to any earlier bath."),
        new("sprint-zero", "Sprint Zero", DuckColour.Mint, 5.00m, 17,
            ["agile"], "Nothing works yet, but it floats beautifully."),
        new("pull-request", "Pull Request", DuckColour.Mint, 6.00m, 23,
            ["git"], "Waiting for review since Tuesday."),
    ];
}
