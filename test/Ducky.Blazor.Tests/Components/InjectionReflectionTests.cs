using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Ducky.Blazor.Tests.Components;

// Found by the Interactive Auto sample (M14-02): Blazor's injector sets [Inject] properties by reflection, and its trimming
// annotation on the component type keeps the non-public properties of that type only, never of its base classes. A
// trimmed WebAssembly publish (every Release publish: Ducky.Blazor is trimmable) therefore dropped the private setters of
// DuckyComponent.Store/Services and DuckyLayout.Store/Services, and every Ducky component threw "Cannot provide a value
// for property 'Store' ... because the property has no setter" in the browser. Each base keeps them through a
// DynamicDependency on a field every derived component initializes (a type-level DynamicallyAccessedMembers is applied
// lazily, only to hierarchies reached through GetType(), and was measured not to keep them). The trimmer keeps such a
// dependency only when it marks the field, which it does through the initializer every derived constructor runs, so this
// test checks the declaration and that the carrying field is initialized; it cannot see trimming. The E2E spec
// ServerSample_WasmRenderMode_ComponentInjectsRendersAndDispatches checks the trimmed publish itself and is the arbiter.
public sealed class InjectionReflectionTests
{
    [Theory]
    [InlineData(typeof(DuckyComponent), typeof(ComponentProbe))]
    [InlineData(typeof(DuckyLayout), typeof(LayoutProbe))]
    public void Inject_NonPublicSettersOfBaseComponent_DeclaredAsDynamicDependency(Type component, Type derived)
    {
        var injected = component.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(p => p.IsDefined(typeof(InjectAttribute)))
            .ToList();
        var carriers = component.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(f => f.IsDefined(typeof(DynamicDependencyAttribute)))
            .ToList();
        var dependencies = carriers.SelectMany(f => f.GetCustomAttributes<DynamicDependencyAttribute>()).ToList();
        var instance = Activator.CreateInstance(derived);

        injected.Select(p => p.Name).ShouldBe(["Store", "Services"], ignoreOrder: true);
        injected.ShouldAllBe(p => p.SetMethod != null && !p.SetMethod.IsPublic);
        dependencies.ShouldContain(d => d.Type == component && d.MemberTypes.HasFlag(DynamicallyAccessedMemberTypes.NonPublicProperties));
        // The trimmer marks the field only through the initializer a derived constructor runs: a reference field that a
        // fresh instance leaves null is never marked, and its dependency is dropped with it.
        carriers.ShouldAllBe(f => !f.FieldType.IsValueType && f.GetValue(instance) != null);
    }

    private sealed class ComponentProbe : DuckyComponent;

    private sealed class LayoutProbe : DuckyLayout;
}
