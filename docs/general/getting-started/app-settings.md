# AppSettings

The `appsettings.json` file in an ASP.NET Core application is used to configure application settings such as logging levels, allowed hosts, and custom configuration sections. Here is an example of a typical `appsettings.json` file used by the Ducky framework:

Ducky uses the `appsettings.json` configuration to set up and register services.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Ducky": "Debug",
      "Ducky.Blazor": "Debug"
    }
  },
  "AllowedHosts": "*",
  "Ducky": {
    "AssemblyNames": [
      "Demo.App", // Your website
      "Demo.AppStore" // If you have your Store in another assembly
    ]
  }
}
```

## Configuration Sections

### Logging

The `Logging` section configures the logging levels for different parts of the application. The `LogLevel` subsection specifies the minimum log level for different logging categories.

- `Ducky` and `Ducky.Blazor`: The logging levels specifically for the Ducky framework and Blazor components.

### AllowedHosts

The `AllowedHosts` setting specifies the hosts that are allowed to access the application. The wildcard `*` allows any host.

### Ducky

The `Ducky` section contains custom settings specific to the Ducky framework:

- `AssemblyNames`: An array of assembly names that the Ducky framework will scan for slices and effects. This allows the framework to dynamically discover and register services.
