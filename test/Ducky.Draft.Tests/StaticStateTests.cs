using Ducky.TestSupport;

namespace Ducky.Draft.Tests;

public sealed class StaticStateTests
{
    [Fact]
    public void NoStaticMutableFields() =>
        StaticFieldAudit.Violations(typeof(DraftableAttribute).Assembly).ShouldBeEmpty();
}
