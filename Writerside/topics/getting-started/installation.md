# Installation

This page will guide you through the process of installing Ducky in your .NET applications. Follow the steps below to
get started quickly and easily.

## Prerequisites

Before you install Ducky, ensure that you have the following prerequisites:

<procedure title="Prerequisites">
    <step>
        <p><control>.NET SDK</control>: Ducky requires .NET SDK version 9.0 or later. You can download and install the .NET SDK from the official <a href="https://dotnet.microsoft.com/download">.NET website</a>.</p>
        <code-block lang="bash">
        dotnet --version
        </code-block>
        <p>This command should return a version number of 9.0 or later.</p>
    </step>
    <step>
        <p><control>Integrated Development Environment (IDE)</control>: While you can use any text editor to develop .NET applications, we recommend using an IDE like <a href="https://visualstudio.microsoft.com/">Visual Studio</a>, <a href="https://code.visualstudio.com/">Visual Studio Code</a>, or <a href="https://www.jetbrains.com/rider/">Rider</a> for a better development experience.</p>
    </step>
</procedure>

## Installing via NuGet

Ducky is available as a NuGet package. You can install it using the NuGet Package Manager in Visual Studio, the .NET
CLI, or the Package Manager Console.

<procedure title="Using the .NET CLI">
    <step><p>Open a terminal or command prompt.</p></step>
    <step><p>Navigate to your project directory.</p></step>
    <step>
        <p>Run the following command to install Ducky:</p>
        <code-block lang="bash">
        dotnet add package Ducky
        dotnet add package Ducky.Blazor
        dotnet add package Ducky.Generator
        </code-block>
    </step>
</procedure>

<procedure title="Using the Package Manager Console">
    <step><p>Open your solution in Visual Studio.</p></step>
    <step><p>Go to <control>Tools</control> &gt; <control>NuGet Package Manager</control> &gt; <control>Package Manager Console</control>.</p></step>
    <step>
        <p>Run the following command in the Package Manager Console:</p>
        <code-block lang="powershell">
        Install-Package Ducky
        Install-Package Ducky.Blazor
        Install-Package Ducky.Generator
        </code-block>
    </step>
</procedure>

<procedure title="Using the NuGet Package Manager in Visual Studio">
    <step><p>Open your solution in Visual Studio.</p></step>
    <step><p>Right-click on your project in the <control>Solution Explorer</control> and select <control>Manage NuGet Packages</control>.</p></step>
    <step><p>In the <control>NuGet Package Manager</control>, search for &quot;Ducky&quot;.</p></step>
    <step><p>Select the Ducky packages from the list and click <control>Install</control>.</p></step>
</procedure>

## Verifying the Installation

To verify that Ducky is installed correctly, you can check your project's dependencies:

<procedure>
<step><p>Open your project file (e.g., <code>.csproj</code>).</p></step>
<step><p>Ensure that there is a reference to Ducky:</p>
    <code-block lang="xml">
    &lt;ItemGroup&gt;
      &lt;PackageReference Include=&quot;Ducky&quot; Version=&quot;1.0.0&quot; /&gt;
      &lt;PackageReference Include=&quot;Ducky.Blazor&quot; Version=&quot;1.0.0&quot; /&gt;
      &lt;PackageReference Include=&quot;Ducky.Generator&quot; Version=&quot;1.0.0&quot; /&gt;
    &lt;/ItemGroup&gt;
    </code-block>
</step>
</procedure>

Once you have completed these steps, Ducky should be installed and ready to use in your project. You can now start
integrating Ducky into your .NET applications to manage state effectively.
