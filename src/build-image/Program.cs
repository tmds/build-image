using System.CommandLine;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

[assembly:InternalsVisibleTo("BuildImage.Tests")]

public class Program
{
    public static int Main(string[] args)
    {
        MSBuildLocator.RegisterDefaults();
        return new BuildCommand().Invoke(args);
    }
}