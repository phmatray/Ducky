using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace R3dux;

public static class DependencyInjections
{
    public static IServiceCollection AddR3dux(
        this IServiceCollection services,
        Action<R3duxOptions> configureOptions)
    {
        R3duxOptions options = new();
        configureOptions(options);

        services.AddBlazorR3();
        services.AddSingleton<IDispatcher, Dispatcher>();
        
        // Scan and register all Slices
        services.Scan(scan => scan
            .FromAssemblies(options.Assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(Slice<>)))
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // Scan and register all Effects
        services.Scan(scan => scan
            .FromAssemblies(options.Assemblies)
            .AddClasses(classes => classes.AssignableTo<Effect>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
        
        services.AddSingleton(sp =>
        {
            var dispatcher = sp.GetRequiredService<IDispatcher>();
            var slices = sp.GetServices<ISlice>();
            var effects = sp.GetServices<IEffect>();

            return StoreFactory.CreateStore(dispatcher, slices, effects);
        });
        
        return services;
    }
}

public class R3duxOptions
{
    public Assembly[] Assemblies { get; set; } = [Assembly.GetExecutingAssembly()];
}

public static class StoreFactory
{
    public static Store CreateStore(
        IDispatcher dispatcher,
        IEnumerable<ISlice> slices,
        IEnumerable<IEffect> effects)
    {
        var store = new Store(dispatcher);

        store.AddSlices(slices);
        store.AddEffects(effects);

        return store;
    }
}