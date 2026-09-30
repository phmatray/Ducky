using Ducky.TestSupport;

namespace Ducky.Draft.Generators.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(DraftGenerator).Assembly).ShouldBeEmpty();
}
