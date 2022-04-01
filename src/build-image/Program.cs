using System.CommandLine;
using Microsoft.Build.Locator;

public class Program
{
    public static int Main(string[] args)
    {
        MSBuildLocator.RegisterDefaults();
        return new BuildCommand().Invoke(args);
    }
}