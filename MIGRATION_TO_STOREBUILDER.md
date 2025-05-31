# Migration Guide: Moving to StoreBuilder API

This guide helps you migrate from the old middleware registration methods to the new StoreBuilder API.

## Overview

The StoreBuilder API provides a cleaner, more maintainable way to configure Ducky with better type safety and IntelliSense support.

## Basic Migration

### Old Way
```csharp
// Register middlewares individually
services.AddCorrelationIdMiddleware();
services.AddExceptionHandlingMiddleware();
services.AddAsyncEffectMiddleware();
services.AddReactiveEffectMiddleware();

// Add Ducky with pipeline configuration
services.AddDucky(options =>
{
    options.ConfigurePipelineWithServices = (pipeline, sp) =>
    {
        pipeline.Use(sp.GetRequiredService<CorrelationIdMiddleware>());
        pipeline.Use(sp.GetRequiredService<ExceptionHandlingMiddleware>());
        // ... more middleware
    };
});
```

### New Way
```csharp
services.AddDuckyStore(builder => builder
    .AddCorrelationIdMiddleware()
    .AddExceptionHandlingMiddleware()
    .AddAsyncEffectMiddleware()
    .AddReactiveEffectMiddleware()
);
```

## Blazor Applications

### Old Way
```csharp
services.AddDuckyBlazor(configuration, (pipeline, sp) =>
{
    // Custom pipeline configuration
});
```

### New Way
```csharp
services.AddDuckyBlazor(configuration, builder =>
{
    // Additional configuration
    builder.AddDevToolsMiddleware(options => 
    {
        options.Enabled = true;
        options.StoreName = "MyApp";
    });
});
```

## Adding Effects

### Old Way
```csharp
services.AddAsyncEffect<MyEffect>();
services.AddReactiveEffect<MyReactiveEffect>();
```

### New Way
```csharp
services.AddDuckyStore(builder => builder
    .AddAsyncEffectMiddleware() // Required first
    .AddEffect<MyEffect>()
    .AddReactiveEffectMiddleware() // Required first
    .AddReactiveEffect<MyReactiveEffect>()
);
```

## Custom Middleware

### Old Way
```csharp
services.AddScoped<MyCustomMiddleware>();
services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<MyCustomMiddleware>());
```

### New Way
```csharp
services.AddDuckyStore(builder => builder
    .AddMiddleware<MyCustomMiddleware>()
    // Or with factory
    .AddMiddleware<MyCustomMiddleware>(sp => new MyCustomMiddleware(sp.GetRequiredService<ILogger>()))
);
```

## Blazor-Specific Middlewares

The new API provides dedicated extension methods for Blazor middlewares:

```csharp
services.AddDuckyStore(builder => builder
    .AddJsLoggingMiddleware()
    .AddDevToolsMiddleware(options => 
    {
        options.StoreName = "MyApp";
        options.Enabled = isDevelopment;
        options.ExcludedActionTypes = ["Tick"];
    })
    .AddPersistenceMiddleware(options =>
    {
        options.StateKey = "app-state";
        options.PersistInterval = TimeSpan.FromSeconds(1);
    })
);
```

## Complete Example

```csharp
services.AddDuckyStore(builder => builder
    // Core middlewares
    .AddCorrelationIdMiddleware()
    .AddExceptionHandlingMiddleware()
    
    // Effect middlewares
    .AddAsyncEffectMiddleware()
    .AddReactiveEffectMiddleware()
    
    // Blazor-specific middlewares
    .AddDevToolsMiddleware(options => options.Enabled = isDevelopment)
    
    // Add effects
    .AddEffect<LoadDataEffect>()
    .AddReactiveEffect<AutoSaveEffect>()
    
    // Add exception handlers
    .AddExceptionHandler<GlobalExceptionHandler>()
    
    // Configure options
    .ConfigureOptions(options =>
    {
        options.AssemblyNames = ["MyApp.Store"];
    })
);
```

## Benefits of Migration

1. **Better IntelliSense** - Fluent API provides better discoverability
2. **Type Safety** - Compile-time checking for middleware dependencies
3. **Cleaner Code** - Less boilerplate and manual service registration
4. **Dependency Management** - Automatic registration of required services
5. **Validation** - Built-in checks for middleware ordering (e.g., effects require effect middleware)

## Removed APIs

The following extension methods have been removed:
- `AddDucky()` (all overloads)
- `AddDuckyWithMiddleware()`
- `AddDuckyWithPipeline()`
- All individual middleware registration methods (`Add*Middleware()`)

Use `AddDuckyStore()` with the StoreBuilder API instead.