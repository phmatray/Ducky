using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky;

// DUCKY309 (§5.1, INV-31): at first resolution, each type the store will build with ActivatorUtilities, or that an
// extension queued, must have one candidate constructor whose every parameter without a default value resolves. IStore,
// IDispatcher and slice types need no special case: AddDucky registers them, so IsService already answers true.
internal static class CtorCheck
{
    public static IEnumerable<DuckyError> Run(IEnumerable<Requirement> requirements, IServiceProvider services, SafeLogger logger)
    {
        var isService = services.GetService<IServiceProviderIsService>();
        var isKeyedService = services.GetService<IServiceProviderIsKeyedService>();
        // Not deconstructed: the generated Deconstruct's out parameter carries no annotation, so ILC reports IL2070 on
        // GetConstructors below (the Roslyn trim analyzer misses it; the AotSmoke publish caught it).
        foreach (var requirement in requirements)
        {
            var type = requirement.Type;
            var requiredBy = requirement.RequiredBy;
            if (isService is null)
            {
                Log.CtorCheckSkipped(logger, type, typeof(IServiceProviderIsService));
                continue;
            }

            // The candidates ActivatorUtilities picks from: the [ActivatorUtilitiesConstructor] one, else every public one.
            var constructors = type.GetConstructors();
            var marked = Array.Find(constructors, c => c.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute)));
            List<DuckyError?> failures = [.. (marked is null ? constructors : [marked])
                .Select(constructor => constructor.GetParameters().Select(Missing).FirstOrDefault(error => error is not null))];

            // A candidate that resolves passes. No public constructor leaves nothing to name: materialization reports it (DUCKY353).
            if (!failures.Contains(null) && failures.FirstOrDefault() is { } error)
            {
                yield return error;
            }

            DuckyError? Missing(ParameterInfo parameter)
            {
                if (parameter.HasDefaultValue)
                {
                    return null;
                }

                if (parameter.IsDefined(typeof(ServiceKeyAttribute)))
                {
                    return DuckyErrors.ServiceKeyParameter(type, requiredBy, parameter.Name!);
                }

                if (parameter.GetCustomAttribute<FromKeyedServicesAttribute>() is { } keyed)
                {
                    if (isKeyedService is null)
                    {
                        Log.CtorCheckSkipped(logger, type, typeof(IServiceProviderIsKeyedService));
                        return null;
                    }

                    return isKeyedService.IsKeyedService(parameter.ParameterType, keyed.Key)
                        ? null
                        : DuckyErrors.UnresolvableConstructor(type, requiredBy, parameter.ParameterType, keyed.Key);
                }

                return isService.IsService(parameter.ParameterType)
                    ? null
                    : DuckyErrors.UnresolvableConstructor(type, requiredBy, parameter.ParameterType, null);
            }
        }
    }

    // A type to check and what registered it, named in the error. The annotation keeps its constructors under trimming; it
    // is on the parameter too, or ILC reports IL2069 for the generated constructor's store into the annotated field.
    internal sealed record Requirement(
        [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type Type,
        string RequiredBy);
}
