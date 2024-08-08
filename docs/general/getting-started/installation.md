# Installation

This page will guide you through the process of installing R3dux in your .NET applications. Follow the steps below to get started quickly and easily.

## Prerequisites

Before you install R3dux, ensure that you have the following prerequisites:

1. **.NET SDK**: R3dux requires .NET SDK version 8.0 or later. You can download and install the .NET SDK from the official [.NET website](https://dotnet.microsoft.com/download).

    ```bash
    dotnet --version
    ```

   This command should return a version number of 8.0 or later.

2. **Integrated Development Environment (IDE)**: While you can use any text editor to develop .NET applications, we recommend using an IDE like [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or [Rider](https://www.jetbrains.com/rider/) for a better development experience.

## Installing via NuGet

R3dux is available as a NuGet package. You can install it using the NuGet Package Manager in Visual Studio, the .NET CLI, or the Package Manager Console.

### Using the .NET CLI

1. Open a terminal or command prompt.
2. Navigate to your project directory.
3. Run the following command to install R3dux:

    ```bash
    dotnet add package R3dux
    dotnet add package R3dux.Blazor
    ```

### Using the Package Manager Console in Visual Studio

1. Open your solution in Visual Studio.
2. Go to **Tools** > **NuGet Package Manager** > **Package Manager Console**.
3. Run the following command in the Package Manager Console:

    ```powershell
    Install-Package R3dux
    Install-Package R3dux.Blazor
    ```

### Using the NuGet Package Manager in Visual Studio

1. Open your solution in Visual Studio.
2. Right-click on your project in the **Solution Explorer** and select **Manage NuGet Packages**.
3. In the **NuGet Package Manager**, search for "R3dux".
4. Select the R3dux package from the list and click **Install**.

## Verifying the Installation

To verify that R3dux is installed correctly, you can check your project's dependencies:

1. Open your project file (e.g., `.csproj`).
2. Ensure that there is a reference to R3dux:

    ```xml
    <ItemGroup>
      <PackageReference Include="R3dux" Version="1.0.0" />
      <PackageReference Include="R3dux.Blazor" Version="1.0.0" />
    </ItemGroup>
    ```

Once you have completed these steps, R3dux should be installed and ready to use in your project. You can now start integrating R3dux into your .NET applications to manage state effectively.
