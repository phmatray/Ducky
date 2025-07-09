using Shouldly;
using Xunit;

namespace Ducky.Tests;

public class DuckyVersioningTests
{
    [Fact]
    public void GetVersion_ShouldReturnNonNullVersion()
    {
        // Act
        Version version = DuckyVersioning.GetVersion();

        // Assert
        version.ShouldNotBeNull();
    }

    [Fact]
    public void GetVersion_ShouldReturnVersionWithComponents()
    {
        // Act
        Version version = DuckyVersioning.GetVersion();

        // Assert
        version.Major.ShouldBeGreaterThanOrEqualTo(0);
        version.Minor.ShouldBeGreaterThanOrEqualTo(0);
        version.Build.ShouldBeGreaterThanOrEqualTo(0);
        version.Revision.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetVersion_ShouldReturnConsistentVersion()
    {
        // Act
        Version version1 = DuckyVersioning.GetVersion();
        Version version2 = DuckyVersioning.GetVersion();

        // Assert
        version1.ShouldBe(version2);
    }

    [Fact]
    public void GetVersion_ShouldReturnValidVersionString()
    {
        // Act
        Version version = DuckyVersioning.GetVersion();
        string versionString = version.ToString();

        // Assert
        versionString.ShouldNotBeNullOrWhiteSpace();
        versionString.ShouldContain(".");
    }

    [Fact]
    public void GetVersion_ShouldNotReturnDefaultZeroVersion()
    {
        // Act
        Version version = DuckyVersioning.GetVersion();

        // Assert
        // In a real assembly, the version should not be 0.0.0.0
        // If it is, it's likely using the default
        bool isDefaultVersion = version.Major == 0 && version.Minor == 0 && 
                               version.Build == 0 && version.Revision == 0;
        
        // This assertion might fail in test environment but documents expected behavior
        // In production, the assembly should have a proper version
        isDefaultVersion.ShouldBeFalse("Assembly should have a proper version set");
    }
}