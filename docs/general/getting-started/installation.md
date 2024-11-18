# Installation

This page will guide you through the process of installing Ducky in your .NET applications. Follow the steps below to get started quickly and easily.

## Prerequisites

Before you install Ducky, ensure that you have the following prerequisites:

1. **.NET SDK**: Ducky requires .NET SDK version 8.0 or later. You can download and install the .NET SDK from the official [.NET website](https://dotnet.microsoft.com/download).

    ```bash
    dotnet --version
    ```

   This command should return a version number of 8.0 or later.

2. **Integrated Development Environment (IDE)**: While you can use any text editor to develop .NET applications, we recommend using an IDE like [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or [Rider](https://www.jetbrains.com/rider/) for a better development experience.

## Installing via NuGet

Ducky is available as a NuGet package. You can install it using the NuGet Package Manager in Visual Studio, the .NET CLI, or the Package Manager Console.

### Using the .NET CLI

1. Open a terminal or command prompt.
2. Navigate to your project directory.
3. Run the following command to install Ducky:

    ```bash
    dotnet add package Ducky
    dotnet add package Ducky.Blazor
    ```

### Using the Package Manager Console in Visual Studio

1. Open your solution in Visual Studio.
2. Go to **Tools** > **NuGet Package Manager** > **Package Manager Console**.
3. Run the following command in the Package Manager Console:

    ```powershell
    Install-Package Ducky
    Install-Package Ducky.Blazor
    ```

### Using the NuGet Package Manager in Visual Studio

1. Open your solution in Visual Studio.
2. Right-click on your project in the **Solution Explorer** and select **Manage NuGet Packages**.
3. In the **NuGet Package Manager**, search for "Ducky".
4. Select the Ducky package from the list and click **Install**.

## Verifying the Installation

To verify that Ducky is installed correctly, you can check your project's dependencies:

1. Open your project file (e.g., `.csproj`).
2. Ensure that there is a reference to Ducky:

    ```xml
    <ItemGroup>
      <PackageReference Include="Ducky" Version="1.0.0" />
      <PackageReference Include="Ducky.Blazor" Version="1.0.0" />
    </ItemGroup>
    ```

Once you have completed these steps, Ducky should be installed and ready to use in your project. You can now start integrating Ducky into your .NET applications to manage state effectively.
