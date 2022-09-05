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
    [InlineData("ubi", "6.0")]
    // Microsoft default
    [InlineData("", "6.0")]
    // Microsoft alpine
    [InlineData("alpine", "6.0")]
    // Microsoft jammy-chiseled
    [InlineData("jammy-chiseled", "6.0")]
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

    static BuildTests()
    {
        MSBuildLocator.RegisterDefaults();
    }
}