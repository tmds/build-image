using Xunit;
using System.CommandLine;
using System.IO;
using Microsoft.Build.Locator;
using System.Diagnostics;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace BuildImage.Tests;

public class BuildTests
{
    // Red Hat ubi
    [InlineData("ubi", "7.0")]
    // Microsoft default
    [InlineData("", "7.0")]
    // Microsoft alpine
    [InlineData("alpine", "7.0")]
    // Microsoft jammy-chiseled
    // [InlineData("jammy-chiseled", "7.0")] // Chiseled images are not yet available for .NET 7.0.
    [Theory]
    public async Task Build(string flavor, string version)
    {
        // Build image.
        string imageTag = $"build-test-{flavor}-{version}";
        string projectFolder = $"webprojects/{version}";
        string[] args = new[] { "-b", flavor, "-t", imageTag, projectFolder };
        int rv = new BuildCommand().Invoke(args);
        Assert.Equal(0, rv);

        // Start container.
        Process container = Process.Start("podman", $"run -p 8080:8080 {imageTag}");
        container.Start();

        try
        {
            // Make HTTP request.
            using var client = new HttpClient();
            for (int i = 10; i >= 0; i--)
            {
                try
                {
                    await client.GetAsync("http://localhost:8080");
                    break; // Success
                }
                catch (HttpRequestException) when (i > 0) // Try again.
                {
                    await Task.Delay(1000);
                }
            }
        }
        finally
        {
            container.Kill();
            container.WaitForExit();
        }
    }

    // Red Hat ubi
    [InlineData("ubi", "s390x", "7.0")]
    // Microsoft default
    [InlineData("", "arm64", "7.0")]
    [Theory]
    public async Task Architecture(string flavor, string arch, string version)
    {
        string imageTag = $"build-test-{flavor}-{arch}-{version}";
        string projectFolder = $"webprojects/{version}";
        string[] args = new[] { "-b", flavor, "-t", imageTag, "--arch", arch, projectFolder };
        int rv = new BuildCommand().Invoke(args);
        Assert.Equal(0, rv);
    }

    static BuildTests()
    {
        MSBuildLocator.RegisterDefaults();
    }
}