using Xunit;

namespace BuildImage.Tests;

public class ImageFlavors
{
    // Red Hat ubi
    [InlineData("ubi", "3.1", "registry.access.redhat.com/ubi8/dotnet-31-runtime:latest", "3.1", "registry.access.redhat.com/ubi8/dotnet-31:latest")]
    [InlineData("ubi", "3.1", "registry.access.redhat.com/ubi8/dotnet-31-runtime:latest", "6.0", "registry.access.redhat.com/ubi8/dotnet-60:latest")]
    [InlineData("ubi", "6.0", "registry.access.redhat.com/ubi8/dotnet-60-runtime:latest", "6.0", "registry.access.redhat.com/ubi8/dotnet-60:latest")]
    // Microsoft default
    [InlineData("", "3.1", "mcr.microsoft.com/dotnet/aspnet:3.1", "3.1", "mcr.microsoft.com/dotnet/sdk:3.1")]
    [InlineData("", "3.1", "mcr.microsoft.com/dotnet/aspnet:3.1", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0")]
    [InlineData("", "6.0", "mcr.microsoft.com/dotnet/aspnet:6.0", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0")]
    // Microsoft alpine
    [InlineData("alpine", "3.1", "mcr.microsoft.com/dotnet/aspnet:3.1-alpine", "3.1", "mcr.microsoft.com/dotnet/sdk:3.1-alpine")]
    [InlineData("alpine", "3.1", "mcr.microsoft.com/dotnet/aspnet:3.1-alpine", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0-alpine")]
    [InlineData("alpine", "6.0", "mcr.microsoft.com/dotnet/aspnet:6.0-alpine", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0-alpine")]
    // Microsoft jammy-chiseled
    [InlineData("jammy-chiseled", "6.0", "mcr.microsoft.com/dotnet/nightly/aspnet:6.0-jammy-chiseled", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0-jammy")]
    // Name with repository
    [InlineData("some.repository.com/repo/runtime", "6.0", "some.repository.com/repo/runtime:6.0", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0")]
    [InlineData("some.repository.com/repo/runtime:1.0-alpine", "6.0", "some.repository.com/repo/runtime:1.0-alpine", "6.0", "mcr.microsoft.com/dotnet/sdk:6.0")]
    [InlineData("registry.access.redhat.com/ubi8/dotnet-60-runtime", "6.0", "registry.access.redhat.com/ubi8/dotnet-60-runtime:latest", "6.0", "registry.access.redhat.com/ubi8/dotnet-60:latest")]
    [Theory]
    public void Flavors(string flavor, string runtimeVersion, string runtimeImage, string sdkVersion, string sdkImage)
    {
        var resolvedImages = ImageDatabase.Resolve(new Flavor(flavor), null, runtimeVersion, sdkVersion);
        Assert.Equal(runtimeImage, resolvedImages.BaseImage);
        Assert.Equal(sdkImage, resolvedImages.SdkImage);
    }
}